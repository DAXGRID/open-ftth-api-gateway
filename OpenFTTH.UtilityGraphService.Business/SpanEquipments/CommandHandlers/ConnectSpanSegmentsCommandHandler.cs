using DAX.EventProcessing;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Changes;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenFTTH.RouteNetwork.Business.Interest;
using OpenFTTH.RouteNetwork.Business.Interest.Projections;
using OpenFTTH.RouteNetwork.Business.RouteElements.StateHandling;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class ConnectSpanSegmentsCommandHandler : ICommandHandler<ConnectSpanSegmentsAtRouteNode, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly IExternalEventProducer _externalEventProducer;
        private readonly UtilityNetworkProjection _utilityNetwork;
        private readonly LookupCollection<SpanEquipmentSpecification> _spanEquipmentSpecifications;
        private readonly IRouteNetworkRepository _routeNetworkRepository;

        public ConnectSpanSegmentsCommandHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer, IRouteNetworkRepository routeNodeRepository)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = externalEventProducer;
            _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            _spanEquipmentSpecifications = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;
            _routeNetworkRepository = routeNodeRepository;

        }

        public Task<Result> HandleAsync(ConnectSpanSegmentsAtRouteNode command)
        {
            if (command.SpanSegmentsToConnect.Length == 0)
                return Task.FromResult(Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(ConnectSpanSegmentsAtRouteNodeErrorCodes.INVALID_SPAN_SEGMENT_LIST_CANNOT_BE_EMPTY, "A list of span segments to connect must be provided.")));

            // Build supporting structure holding information on how span equipments are to be connected to each other
            var spanEquipmentsToConnectBuilderResult = BuildSpanEquipmentsToConnect(command);

            if (spanEquipmentsToConnectBuilderResult.IsFailed)
                return Task.FromResult(Result.Fail(spanEquipmentsToConnectBuilderResult.Errors.First()));

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            var spanEquipmentsToConnect = spanEquipmentsToConnectBuilderResult.Value;

            foreach (var spanSegmentToConnect in command.SpanSegmentsToConnect)
            {
                if (_utilityNetwork.CheckIfConduitSegmentContainsCables(spanSegmentToConnect))
                    return Task.FromResult(Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(ConnectSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_CONTAIN_CABLE, $"The span segment id: {spanSegmentToConnect} contain a cable. Cannot be connected.")));
            }

            if (spanEquipmentsToConnect.Count == 1)
            {
                var spanEquipmentToConnect = spanEquipmentsToConnect.Values.First();

                // If the user re-connects the outer span, the entire span equipment (both outer and all inner spans) will be uncut at the node
                if (spanEquipmentToConnect.Connects.Count == 2 && spanEquipmentToConnect.Connects[0].StructureIndex == 0 && spanEquipmentToConnect.Connects[1].StructureIndex == 0)
                {
                    var connectResult = RevertSpanEquipmentCut(commandContext, command.RouteNodeId, spanEquipmentToConnect.SpanEquipment.Id);
                    return Task.FromResult(connectResult);
                }
                else
                {
                    var connectResult = ConnectSpanSegmentsInSameEquipment(commandContext, command.RouteNodeId, spanEquipmentToConnect);
                    return Task.FromResult(connectResult);
                }
            }
            else if (spanEquipmentsToConnect.Count == 2)
            {
                var firstSpanEquipment = spanEquipmentsToConnect.Values.First();
                var secondSpanEquipment = spanEquipmentsToConnect.Values.Last();

                // Check that number of span segments from each span equipment is the same
                if (firstSpanEquipment.Connects.Count != secondSpanEquipment.Connects.Count)
                {
                    return Task.FromResult(
                        Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(
                            ConnectSpanSegmentsAtRouteNodeErrorCodes.EXPECTED_SAME_NUMBER_OF_SPAN_SEGMENTS_BELONGING_TO_TWO_SPAN_EQUIPMENT,
                            $"Cannot connect the span segments specified because {firstSpanEquipment.Connects.Count} span segments are selected from span equipment: {firstSpanEquipment.SpanEquipment.Id} and {secondSpanEquipment.Connects.Count} span segments are selected from span equipment: {secondSpanEquipment.SpanEquipment.Id} The number of span segments selected in the two span equipments must the same!")
                        )
                    );
                }

                // Check that the conduits selected in the two span equipment are alligned in terms of numbers and type
                var allignedResult = CheckIfConnectsAreAlligned(firstSpanEquipment, secondSpanEquipment);

                if (allignedResult.IsFailed)
                    return Task.FromResult(Result.Fail(allignedResult.Errors.First()));

                // Check if a merge should be done instead of connecting the individually spans using junctions/terminals
                if (ShouldBeMerged(command.RouteNodeId, firstSpanEquipment, secondSpanEquipment))
                {
                    var mergeResult = MergeSpanEquipment(commandContext, command.RouteNodeId, firstSpanEquipment, secondSpanEquipment);

                    return Task.FromResult(mergeResult);
                }
                else
                {
                    // Connect the individual spans using junctions/terminals
                    var connectResult = ConnectSpanSegmentsFromTwoSpanEquipment(commandContext, command.RouteNodeId, firstSpanEquipment, secondSpanEquipment);

                    return Task.FromResult(connectResult);
                }
            }
            else
            {
                return Task.FromResult(
                   Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(
                       ConnectSpanSegmentsAtRouteNodeErrorCodes.EXPECTED_SPAN_SEGMENTS_FROM_ONE_OR_TWO_SPAN_EQUIPMENT,
                       $"Got span segments belonging to {spanEquipmentsToConnect.Count} This command can only handle connecting span segments between one or two span equipments.")
                   )
               );
            }
        }

        private Result MergeSpanEquipment(CommandContext cmdContext, Guid routeNodeId, SpanEquipmentWithConnectsHolder firstSpanEquipment, SpanEquipmentWithConnectsHolder secondSpanEquipment)
        {
            // Merge the first span equipment with the second one
            var firstSpanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(firstSpanEquipment.SpanEquipment.Id);

            var firstSpanEquipmentConnectResult = firstSpanEquipmentAR.Merge(
                cmdContext: cmdContext,
                routeNodeId: routeNodeId,
                secondSpanEquipment.SpanEquipment
            );

            if (firstSpanEquipmentConnectResult.IsFailed)
                return firstSpanEquipmentConnectResult;

            var firstSpanEquipmentWalk = GetInterestInformation(firstSpanEquipment.SpanEquipment);
            var secondSpanEquipmentWalk = GetInterestInformation(secondSpanEquipment.SpanEquipment);

            // Update interest of the first span equipment to cover both span equipments
            var newSegmentIds = MergeWalks(firstSpanEquipmentWalk, secondSpanEquipmentWalk);

            var interestProjection = _eventStore.Projections.Get<InterestsProjection>();

            var firstSpanEquipmentInterestAR = _eventStore.Aggregates.Load<InterestAR>(firstSpanEquipment.SpanEquipment.WalkOfInterestId);

            OpenFTTH.RouteNetwork.Business.CommandContext routeNetworkCommandContext = new RouteNetwork.Business.CommandContext(cmdContext.CorrelationId, cmdContext.CmdId, cmdContext.UserContext);

            var walkOfInterest = new RouteNetworkInterest(firstSpanEquipment.SpanEquipment.WalkOfInterestId, RouteNetworkInterestKindEnum.WalkOfInterest, newSegmentIds);

            var updateInterestResult = firstSpanEquipmentInterestAR.UpdateRouteNetworkElements(routeNetworkCommandContext, walkOfInterest, interestProjection, new WalkValidator(_routeNetworkRepository));

            if (updateInterestResult.IsFailed)
                throw new ApplicationException($"Failed to update interest: {firstSpanEquipment.SpanEquipment.WalkOfInterestId} of span equipment: {firstSpanEquipment.SpanEquipment.Id} in ConnectSpanSegmentsCommandHandler Error: {updateInterestResult.Errors.First().Message}");


            // Remove the second span equipment
            var secondSpanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(secondSpanEquipment.SpanEquipment.Id);
            var removeSpanEquipmentResult = secondSpanEquipmentAR.Remove(cmdContext);

            if (removeSpanEquipmentResult.IsFailed)
                return removeSpanEquipmentResult;


            // Remember to remove the walk of interest of second span equipment as well
            var secondSpanEquipmentInterestAR = _eventStore.Aggregates.Load<InterestAR>(secondSpanEquipment.SpanEquipment.WalkOfInterestId);

            var unregisterInterestResult = secondSpanEquipmentInterestAR.UnregisterInterest(routeNetworkCommandContext, interestProjection, secondSpanEquipment.SpanEquipment.WalkOfInterestId);

            if (unregisterInterestResult.IsFailed)
                throw new ApplicationException($"Failed to unregister interest: {secondSpanEquipment.SpanEquipment.WalkOfInterestId} of span equipment: {secondSpanEquipment.SpanEquipment.Id} in ConnectSpanSegmentsCommandHandler Error: {unregisterInterestResult.Errors.First().Message}");

            // Store everything
            _eventStore.Aggregates.Store(firstSpanEquipmentInterestAR);
            _eventStore.Aggregates.Store(firstSpanEquipmentAR);

            _eventStore.Aggregates.Store(secondSpanEquipmentInterestAR);
            _eventStore.Aggregates.Store(secondSpanEquipmentAR);

            NotifyExternalServicesAboutMerge(firstSpanEquipment.SpanEquipment.Id, updateInterestResult.Value.RouteNetworkElementRefs.ToArray());

            return Result.Ok();
        }

        private RouteNetworkElementIdList MergeWalks(ValidatedRouteNetworkWalk firstSpanEquipmentWalk, ValidatedRouteNetworkWalk secondSpanEquipmentWalk)
        {
            var result = new RouteNetworkElementIdList();

            // first span equipment -> second span equipment
            if (firstSpanEquipmentWalk.ToNodeId == secondSpanEquipmentWalk.FromNodeId)
            {
                result.AddRange(firstSpanEquipmentWalk.SegmentIds);
                result.AddRange(secondSpanEquipmentWalk.SegmentIds);
            }
            // first span equipment -> second span equipment (reversed)
            else if (firstSpanEquipmentWalk.ToNodeId == secondSpanEquipmentWalk.ToNodeId)
            {
                secondSpanEquipmentWalk = secondSpanEquipmentWalk.Reverse();

                result.AddRange(firstSpanEquipmentWalk.SegmentIds);
                result.AddRange(secondSpanEquipmentWalk.SegmentIds);
            }
            // second span equipment -> first span equipment
            else if (firstSpanEquipmentWalk.FromNodeId == secondSpanEquipmentWalk.ToNodeId)
            {
                result.AddRange(secondSpanEquipmentWalk.SegmentIds);
                result.AddRange(firstSpanEquipmentWalk.SegmentIds);
            }
            // second span equipment (reversed) -> first span equipment
            else if (firstSpanEquipmentWalk.FromNodeId == secondSpanEquipmentWalk.FromNodeId)
            {
                secondSpanEquipmentWalk = secondSpanEquipmentWalk.Reverse();

                result.AddRange(secondSpanEquipmentWalk.SegmentIds);
                result.AddRange(firstSpanEquipmentWalk.SegmentIds);
            }
            else
                throw new ApplicationException("Merge Walk logic is broken");

            return result;
        }

        private Result ConnectSpanSegmentsFromTwoSpanEquipment(CommandContext cmdContext, Guid routeNodeId, SpanEquipmentWithConnectsHolder firstSpanEquipment, SpanEquipmentWithConnectsHolder secondSpanEquipment)
        {
            var spanEquipmentSpecifications = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;

            // Create junction/terminal ids used to connect span segments
            for (int i = 0; i < firstSpanEquipment.Connects.Count; i++)
            {
                var junctionId = Guid.NewGuid();

                firstSpanEquipment.Connects[i].ConnectInfo.TerminalId = junctionId;
                firstSpanEquipment.Connects[i].ConnectInfo.ConnectionDirection = SpanSegmentToTerminalConnectionDirection.FromSpanSegmentToTerminal;

                secondSpanEquipment.Connects[i].ConnectInfo.TerminalId = junctionId;
                secondSpanEquipment.Connects[i].ConnectInfo.ConnectionDirection = SpanSegmentToTerminalConnectionDirection.FromTerminalToSpanSegment;
            }

            // Connect the first span equipment to terminals
            var firstSpanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(firstSpanEquipment.SpanEquipment.Id);

            var firstSpanEquipmentConnectResult = firstSpanEquipmentAR.ConnectConduitSpanSegmentsToSimpleTerminals(
                cmdContext: cmdContext,
                specification: spanEquipmentSpecifications[firstSpanEquipment.SpanEquipment.SpecificationId],
                routeNodeId: routeNodeId,
                connects: firstSpanEquipment.Connects.Select(c => c.ConnectInfo).ToArray()
            );

            if (firstSpanEquipmentConnectResult.IsFailed)
                return firstSpanEquipmentConnectResult;

            // Connect the second span equipment to terminals
            var secondSpanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(secondSpanEquipment.SpanEquipment.Id);

            var secondSpanEquipmentConnectResult = secondSpanEquipmentAR.ConnectConduitSpanSegmentsToSimpleTerminals(
                cmdContext: cmdContext,
                routeNodeId: routeNodeId,
                specification: spanEquipmentSpecifications[secondSpanEquipment.SpanEquipment.SpecificationId],
                connects: secondSpanEquipment.Connects.Select(c => c.ConnectInfo).ToArray()
            );

            if (secondSpanEquipmentConnectResult.IsFailed)
                return secondSpanEquipmentConnectResult;

            _eventStore.Aggregates.Store(firstSpanEquipmentAR);
            _eventStore.Aggregates.Store(secondSpanEquipmentAR);

            NotifyExternalServicesAboutConnectivityChange(firstSpanEquipment.SpanEquipment.Id, secondSpanEquipment.SpanEquipment.Id, routeNodeId, "EquipmentConnectivityModification.Connect");

            return Result.Ok();
        }

        private Result ConnectSpanSegmentsInSameEquipment(CommandContext cmdContext, Guid routeNodeId, SpanEquipmentWithConnectsHolder spanEquipmentToConnect)
        {
            var spanEquipmentSpecifications = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;

            if (spanEquipmentToConnect.Connects.Count != 2)
            {
                return
                  Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(
                      ConnectSpanSegmentsAtRouteNodeErrorCodes.SAME_SPAN_EQUIPMENT_CONNECTIONS_MUST_BE_DONE_TWO_SPAN_SEGMENTS_AT_THE_TIME,
                      $"Cannot connect the span segments specified because {spanEquipmentToConnect.Connects.Count} segments are selected. Two span segments were expected.")
                );
            }

            // Get the two segments from graph
            if (!_utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(spanEquipmentToConnect.Connects[0].ConnectInfo.SegmentId, out var utilityGraphSegmentRefToConnect1))
                throw new ApplicationException($"Failed to lookup span segment with id: {spanEquipmentToConnect.Connects[0].ConnectInfo.SegmentId}");

            if (!_utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(spanEquipmentToConnect.Connects[1].ConnectInfo.SegmentId, out var utilityGraphSegmentRefToConnect2))
                throw new ApplicationException($"Failed to lookup span segment with id: {spanEquipmentToConnect.Connects[1].ConnectInfo.SegmentId}");


            // If segment 1 -> (node) -> segment 2
            if (utilityGraphSegmentRefToConnect1.SpanSegment(_utilityNetwork).ToNodeOfInterestIndex == utilityGraphSegmentRefToConnect2.SpanSegment(_utilityNetwork).FromNodeOfInterestIndex)
            {
                spanEquipmentToConnect.Connects[0].ConnectInfo.ConnectionDirection = SpanSegmentToTerminalConnectionDirection.FromSpanSegmentToTerminal;
                spanEquipmentToConnect.Connects[1].ConnectInfo.ConnectionDirection = SpanSegmentToTerminalConnectionDirection.FromTerminalToSpanSegment;
            }
            // If segment 2-> (node)-> segment 1
            else if (utilityGraphSegmentRefToConnect1.SpanSegment(_utilityNetwork).FromNodeOfInterestIndex == utilityGraphSegmentRefToConnect2.SpanSegment(_utilityNetwork).ToNodeOfInterestIndex)
            {
                spanEquipmentToConnect.Connects[0].ConnectInfo.ConnectionDirection = SpanSegmentToTerminalConnectionDirection.FromTerminalToSpanSegment;
                spanEquipmentToConnect.Connects[1].ConnectInfo.ConnectionDirection = SpanSegmentToTerminalConnectionDirection.FromSpanSegmentToTerminal;
            }
            else
            {
                return
                    Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(
                        ConnectSpanSegmentsAtRouteNodeErrorCodes.SAME_SPAN_EQUIPMENT_U_TURN_CONNECTIONS_NOT_ALLOWED,
                        $"Cannot connect the span segments specified because the two span segments selected will create an u-turn.")
                );
            }


            // Create junction/terminal ids used to connect span segments
            var junctionId = Guid.NewGuid();

            foreach (var connect in spanEquipmentToConnect.Connects)
            {
                connect.ConnectInfo.TerminalId = junctionId;
            }

            // Connect span equipment to terminals
            var spanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(spanEquipmentToConnect.SpanEquipment.Id);

            var spanEquipmentConnectResult = spanEquipmentAR.ConnectConduitSpanSegmentsToSimpleTerminals(
                cmdContext: cmdContext,
                routeNodeId: routeNodeId,
                specification: spanEquipmentSpecifications[spanEquipmentToConnect.SpanEquipment.SpecificationId],
                connects: spanEquipmentToConnect.Connects.Select(c => c.ConnectInfo).ToArray()
            );

            if (spanEquipmentConnectResult.IsFailed)
                return spanEquipmentConnectResult;

            _eventStore.Aggregates.Store(spanEquipmentAR);

            NotifyExternalServicesAboutConnectivityChange(spanEquipmentToConnect.SpanEquipment.Id, routeNodeId, "EquipmentConnectivityModification.Connect");

            return Result.Ok();
        }

        private Result RevertSpanEquipmentCut(CommandContext cmdContext, Guid routeNodeId, Guid spanEquipmentId)
        {
            // Connect span equipment to terminals
            var spanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(spanEquipmentId);

            var spanEquipmentUncutResult = spanEquipmentAR.RevertCut(
                cmdContext: cmdContext,
                routeNodeId: routeNodeId,
                spanEquipmentId: spanEquipmentId
            );

            if (spanEquipmentUncutResult.IsFailed)
                return spanEquipmentUncutResult;

            _eventStore.Aggregates.Store(spanEquipmentAR);

            NotifyExternalServicesAboutConnectivityChange(spanEquipmentId, routeNodeId, "EquipmentModification.RevertCut");

            return Result.Ok();
        }


        private Result<Dictionary<Guid, SpanEquipmentWithConnectsHolder>> BuildSpanEquipmentsToConnect(ConnectSpanSegmentsAtRouteNode command)
        {
            Dictionary<Guid, SpanEquipmentWithConnectsHolder> spanEquipmentsToConnect = new Dictionary<Guid, SpanEquipmentWithConnectsHolder>();

            // Check that no inner conduits are connected to outer conduits
            bool innerSpansFound = false;
            bool outerSpansFound = false;

            foreach (var spanSegmentToConnectId in command.SpanSegmentsToConnect)
            {
                if (!_utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(spanSegmentToConnectId, out var spanSegmentGraphElement))
                    return Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(ConnectSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_NOT_FOUND, $"Cannot find any span segment in the utility graph with id: {spanSegmentToConnectId}"));

                var spanEquipment = spanSegmentGraphElement.SpanEquipment(_utilityNetwork);
                var spanEquipmentSpecification = _spanEquipmentSpecifications[spanEquipment.SpecificationId];
                var spanSegment = spanSegmentGraphElement.SpanSegment(_utilityNetwork);

                // Check that the user does not try to connect inner and outer spans

                if (spanSegmentGraphElement.StructureIndex > 0)
                    innerSpansFound = true;

                // Single conduits should also be treated as an inner conduit in this check
                if (!spanEquipmentSpecification.IsMultiLevel && spanSegmentGraphElement.StructureIndex == 0 && spanEquipment.SpanStructures.Length == 1)
                    innerSpansFound = true;

                // Outer conduit in a fixed or non-fixed multilevel conduit - i.e. a pre-manufactured multi conduit or an empty outer conduit
                if (spanEquipmentSpecification.IsMultiLevel && spanSegmentGraphElement.StructureIndex == 0)
                    outerSpansFound = true;

                if (outerSpansFound && innerSpansFound)
                {
                    return Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(ConnectSpanSegmentsAtRouteNodeErrorCodes.OUTER_AND_INNER_SPANS_CANNOT_BE_CONNECTED, $"Connect command contains both inner and outer spans. This is not allowed. You cannot connect an outer span to an inner span or vice versa."));
                }

                if (!spanEquipmentsToConnect.ContainsKey(spanEquipment.Id))
                {
                    spanEquipment.TryGetSpanSegment(spanSegment.Id, out var spanSegmentWithIndexInfo);

                    spanEquipmentsToConnect.Add(
                       spanEquipment.Id,
                        new SpanEquipmentWithConnectsHolder(spanEquipment)
                        {
                            Connects = new List<SpanSegmentConnectHolder> {
                                new SpanSegmentConnectHolder(
                                    new SpanSegmentToSimpleTerminalConnectInfo(
                                        segmentId: spanSegment.Id,
                                        terminalId: Guid.Empty
                                    )
                                )
                                {
                                    StructureIndex = spanSegmentWithIndexInfo.StructureIndex
                                }
                            }
                        }
                    );
                }
                else
                {
                    spanEquipment.TryGetSpanSegment(spanSegment.Id, out var spanSegmentWithIndexInfo);

                    spanEquipmentsToConnect[spanEquipment.Id].Connects.Add(
                        new SpanSegmentConnectHolder(
                            new SpanSegmentToSimpleTerminalConnectInfo(
                                segmentId: spanSegment.Id,
                                terminalId: Guid.Empty
                            )
                        )
                        {
                            StructureIndex = spanSegmentWithIndexInfo.StructureIndex
                        }
                    );
                }
            }

            return Result.Ok(spanEquipmentsToConnect);
        }

        private Result CheckIfConnectsAreAlligned(SpanEquipmentWithConnectsHolder firstSpanEquipment, SpanEquipmentWithConnectsHolder secondSpanEquipment)
        {
            // If more than 2 segments selected in each span equipment, check that they are alligned in terms of specifications
            if (firstSpanEquipment.Connects.Count > 1)
            {
                HashSet<Guid> firstSpanEquipmentStructureSpecIds = new HashSet<Guid>();
                HashSet<Guid> secondSpanEquipmentStructureSpecIds = new HashSet<Guid>();

                foreach (var firstEqSpanSegmentConnect in firstSpanEquipment.Connects)
                {
                    firstSpanEquipment.SpanEquipment.TryGetSpanSegment(firstEqSpanSegmentConnect.ConnectInfo.SegmentId, out var spanSegmentWithIndexInfo);
                    var structureSpecId = firstSpanEquipment.SpanEquipment.SpanStructures[spanSegmentWithIndexInfo.StructureIndex].SpecificationId;
                    firstEqSpanSegmentConnect.StructureSpecificationId = structureSpecId;
                    firstSpanEquipmentStructureSpecIds.Add(structureSpecId);
                }

                foreach (var secondEqSpanSegmentConnect in secondSpanEquipment.Connects)
                {
                    secondSpanEquipment.SpanEquipment.TryGetSpanSegment(secondEqSpanSegmentConnect.ConnectInfo.SegmentId, out var spanSegmentWithIndexInfo);
                    var structureSpecId = secondSpanEquipment.SpanEquipment.SpanStructures[spanSegmentWithIndexInfo.StructureIndex].SpecificationId;
                    secondEqSpanSegmentConnect.StructureSpecificationId = structureSpecId;
                    secondSpanEquipmentStructureSpecIds.Add(structureSpecId);
                }

                foreach (var firstStructureId in firstSpanEquipmentStructureSpecIds)
                {
                    if (!secondSpanEquipmentStructureSpecIds.Contains(firstStructureId))
                    {
                        return Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(
                            ConnectSpanSegmentsAtRouteNodeErrorCodes.EXPECTED_SAME_SPECIFICATIONS_OF_SPAN_SEGMENTS_BELONGING_TO_TWO_SPAN_EQUIPMENT,
                            $"Cannot connect the span segments specified because specifications on the span structures don't allign. Make sure that you connect span segments beloning to structure with the same specs - i.e. a red and blue Ø10 from one span equipment to a red and blue Ø10 in the other span equipment.")
                        );
                    }
                }

                // Order connects by spec id
                firstSpanEquipment.Connects = firstSpanEquipment.Connects.OrderBy(s => s.StructureSpecificationId).ToList();
                secondSpanEquipment.Connects = secondSpanEquipment.Connects.OrderBy(s => s.StructureSpecificationId).ToList();
            }

            return Result.Ok();
        }

        private bool ShouldBeMerged(Guid routeNodeId, SpanEquipmentWithConnectsHolder firstSpanEquipment, SpanEquipmentWithConnectsHolder secondSpanEquipment)
        {
            if (firstSpanEquipment.Connects.Count == 1 && firstSpanEquipment.SpanEquipment.TryGetSpanSegment(firstSpanEquipment.Connects[0].ConnectInfo.SegmentId, out var firstSpanSegmentWithIndexInfo))
            {
                // Don't merge if span equipment is affixed to container
                if (firstSpanEquipment.SpanEquipment.NodeContainerAffixes != null && firstSpanEquipment.SpanEquipment.NodeContainerAffixes.Any(affix => affix.RouteNodeId == routeNodeId))
                    return false;

                // If we're dealing with structure index 0, then the client is connecting the outer span
                if (firstSpanSegmentWithIndexInfo.StructureIndex == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private ValidatedRouteNetworkWalk GetInterestInformation(SpanEquipment spanEquipment)
        {
            // Get interest information from existing span equipment
            var interestQueryResult = _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(new GetRouteNetworkDetails(new InterestIdList() { spanEquipment.WalkOfInterestId })).Result;

            if (interestQueryResult.IsFailed)
                throw new ApplicationException($"Got unexpected error result: {interestQueryResult.Errors.First().Message} trying to query interest information for span equipment: {spanEquipment.Id} walk of interest id: {spanEquipment.WalkOfInterestId}");

            if (interestQueryResult.Value.Interests == null)
                throw new ApplicationException($"Error querying interest information belonging to span equipment with id: {spanEquipment.Id}. No interest information returned from route network service.");

            if (!interestQueryResult.Value.Interests.TryGetValue(spanEquipment.WalkOfInterestId, out var routeNetworkInterest))
                throw new ApplicationException($"Error querying interest information belonging to span equipment with id: {spanEquipment.Id}. No interest information returned from route network service.");

            return new ValidatedRouteNetworkWalk(routeNetworkInterest.RouteNetworkElementRefs);
        }

        private async void NotifyExternalServicesAboutConnectivityChange(Guid firstSpanEquipmentId, Guid secondSpanEquipmentId, Guid routeNodeId, string category)
        {
            var idChangeSets = new List<IdChangeSet>
            {
                new IdChangeSet("SpanEquipment", ChangeTypeEnum.Modification, new Guid[] { firstSpanEquipmentId, secondSpanEquipmentId })
            };

            var updatedEvent =
                new RouteNetworkElementContainedEquipmentUpdated(
                    eventType: typeof(RouteNetworkElementContainedEquipmentUpdated).Name,
                    eventId: Guid.NewGuid(),
                    eventTimestamp: DateTime.UtcNow,
                    applicationName: "UtilityNetworkService",
                    applicationInfo: null,
                    category: category,
                    idChangeSets: idChangeSets.ToArray(),
                    affectedRouteNetworkElementIds: new Guid[] { routeNodeId }
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }

        private async void NotifyExternalServicesAboutConnectivityChange(Guid spanEquipmentId, Guid routeNodeId, string category)
        {
            var idChangeSets = new List<IdChangeSet>
            {
                new IdChangeSet("SpanEquipment", ChangeTypeEnum.Modification, new Guid[] { spanEquipmentId })
            };

            var updatedEvent =
                new RouteNetworkElementContainedEquipmentUpdated(
                    eventType: typeof(RouteNetworkElementContainedEquipmentUpdated).Name,
                    eventId: Guid.NewGuid(),
                    eventTimestamp: DateTime.UtcNow,
                    applicationName: "UtilityNetworkService",
                    applicationInfo: null,
                    category: category,
                    idChangeSets: idChangeSets.ToArray(),
                    affectedRouteNetworkElementIds: new Guid[] { routeNodeId }
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }

        private async void NotifyExternalServicesAboutMerge(Guid spanEquipmentId, Guid[] affectedRouteNetworkElements)
        {
            var idChangeSets = new List<IdChangeSet>
            {
                new IdChangeSet("SpanEquipment", ChangeTypeEnum.Addition, new Guid[] { spanEquipmentId })
            };

            var updatedEvent =
                new RouteNetworkElementContainedEquipmentUpdated(
                    eventType: typeof(RouteNetworkElementContainedEquipmentUpdated).Name,
                    eventId: Guid.NewGuid(),
                    eventTimestamp: DateTime.UtcNow,
                    applicationName: "UtilityNetworkService",
                    applicationInfo: null,
                    category: "EquipmentModification.Merge",
                    idChangeSets: idChangeSets.ToArray(),
                    affectedRouteNetworkElementIds: affectedRouteNetworkElements
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }

        private class SpanEquipmentWithConnectsHolder
        {
            public SpanEquipment SpanEquipment { get; }
            public List<SpanSegmentConnectHolder> Connects { get; set; }

            public SpanEquipmentWithConnectsHolder(SpanEquipment spanEquipment)
            {
                SpanEquipment = spanEquipment;
                Connects = new();
            }
        }

        private class SpanSegmentConnectHolder
        {
            public SpanSegmentToSimpleTerminalConnectInfo ConnectInfo { get; }
            public Guid StructureSpecificationId { get; set; }
            public ushort StructureIndex { get; set; }
            public SpanSegmentConnectHolder(SpanSegmentToSimpleTerminalConnectInfo connectInfo)
            {
                ConnectInfo = connectInfo;
            }
        }
    }
}
