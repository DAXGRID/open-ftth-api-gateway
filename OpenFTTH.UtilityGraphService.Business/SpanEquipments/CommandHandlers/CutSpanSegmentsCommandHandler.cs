using DAX.EventProcessing;
using OpenFTTH.Results;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Changes;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.Business.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.RouteNetwork.Business;
using OpenFTTH.RouteNetwork.Business.Interest.Projections;
using OpenFTTH.RouteNetwork.Business.Interest;
using OpenFTTH.RouteNetwork.Business.RouteElements.StateHandling;
using Baseline;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events;
using OpenFTTH.RouteNetwork.Business.RouteElements.Model;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class CutSpanSegmentsCommandHandler : ICommandHandler<CutSpanSegmentsAtRouteNode, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly IExternalEventProducer _externalEventProducer;
        private readonly IRouteNetworkRepository _routeNetworkRepository;

        public CutSpanSegmentsCommandHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer, IRouteNetworkRepository routeNodeRepository)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = externalEventProducer;
            _routeNetworkRepository = routeNodeRepository;
        }

        public Task<Result> HandleAsync(CutSpanSegmentsAtRouteNode command)
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
            var interestsProjection = _eventStore.Projections.Get<InterestsProjection>();

            if (command.SpanSegmentsToCut.Length == 0)
                return Task.FromResult(Result.Fail(new CutSpanSegmentsAtRouteNodeError(CutSpanSegmentsAtRouteNodeErrorCodes.INVALID_SPAN_SEGMENT_LIST_CANNOT_BE_EMPTY, "A list of span segments to cut must be provided.")));

            // Because the client is not required to provide the span equipment id (that we need to lookup the 
            // aggregate root), we look it up via the utility network graph.
            if (!utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(command.SpanSegmentsToCut[0], out var spanSegmentGraphElement))
            {
                if (utilityNetwork.TryGetEquipment<SpanEquipment>(command.SpanSegmentsToCut[0], out var firstSpanEquipmentChecked))
                {
                    return CutCables(command, utilityNetwork, interestsProjection);
                }
                else
                {
                    return Task.FromResult(Result.Fail(new CutSpanSegmentsAtRouteNodeError(CutSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_NOT_FOUND, $"Cannot find any span segment in the utility graph with id: {command.SpanSegmentsToCut[0]}")));
                }
            }
            else
            {
                return CutConduits(command, utilityNetwork, interestsProjection, spanSegmentGraphElement);
            }
        }

        private Task<Result> CutCables(CutSpanSegmentsAtRouteNode command, UtilityNetworkProjection utilityNetwork, InterestsProjection interestsProjection)
        {
            var spanEquipments = _eventStore.Projections.Get<UtilityNetworkProjection>().SpanEquipmentsByEquipmentId;
            var spanEquipmentSpecifications = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;

            // Check that all are cables
            foreach (var cableId in command.SpanSegmentsToCut)
            {
                utilityNetwork.TryGetEquipment<SpanEquipment>(cableId, out var spanEquipmentToBeChecked);

                if (!spanEquipmentToBeChecked.IsCable)
                    return Task.FromResult(Result.Fail(new CutSpanSegmentsAtRouteNodeError(CutSpanSegmentsAtRouteNodeErrorCodes.EXPECTED_SPAN_EQUIPMENT_TO_BE_CABLE, $"Expected span equipment with id: {command.SpanSegmentsToCut[0]} to be a cable")));
            }

            // Check that only one cable is selected
            if (command.SpanSegmentsToCut.Length > 1)
            {
                    return Task.FromResult(Result.Fail(new CutSpanSegmentsAtRouteNodeError(CutSpanSegmentsAtRouteNodeErrorCodes.EXPECTED_ONLY_ONE_CABLE, $"Expected only one cable to be cut. Cutting multiple cables at once is not allowed.")));
            }
            
            utilityNetwork.TryGetEquipment<SpanEquipment>(command.SpanSegmentsToCut[0], out var cableToBeCut);

            var utilityCmdContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);
            var networkCmdContext = new RouteNetwork.Business.CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            var existingWalk = GetInterestInformation(cableToBeCut);

            // Only cut cables not starting or ending in the node (where to cut)
            if (existingWalk.NodeIds.Contains(command.RouteNodeId) && existingWalk.NodeIds.First() != command.RouteNodeId && existingWalk.NodeIds.Last() != command.RouteNodeId)
            {

                // Disconnect eventually to terminals
                var disconnects = FindCableEndDisconnects(utilityNetwork, cableToBeCut);

                var existingCableEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(cableToBeCut.Id);

                if (disconnects.Count > 0)
                {
                    // Disconnect the segments
                    var disconnectResult = existingCableEquipmentAR.DisconnectSegmentsFromTerminals(
                           cmdContext: utilityCmdContext,
                           disconnects.Values.ToArray()
                    );

                    if (!disconnectResult.IsSuccess)
                    {
                        return Task.FromResult(disconnectResult);
                    }
                }

                ////////////////////////////
                // Shrink existing cable

                var existingCableWalkAfterShrink = GetWalkBeforeNode(existingWalk.RouteNetworkElementRefs, command.RouteNodeId);

                var newCableWalk = GetWalkAfterNode(existingWalk.RouteNetworkElementRefs, command.RouteNodeId);

                if (CheckIfUtilityHopsIsCutInWalk(cableToBeCut.UtilityNetworkHops, command.RouteNodeId, existingCableWalkAfterShrink, newCableWalk))
                    throw new ApplicationException($"Cutting of span equipment: {cableToBeCut.Id} failed. A utility hop span the node where to cut. The cut logic cannot handle that situation.");

                var existingCableNewUtilityNetworkHops = GetUtilityHopsRelatedToWalk(cableToBeCut.UtilityNetworkHops, existingCableWalkAfterShrink);



                // Shrink cable and update update utility hops
                var moveSpanEquipmentResult = existingCableEquipmentAR.Shrink(utilityCmdContext, existingCableWalkAfterShrink, existingWalk, existingCableNewUtilityNetworkHops);

                if (moveSpanEquipmentResult.IsFailed)
                    return Task.FromResult(Result.Fail(moveSpanEquipmentResult.Errors.First()));

                // Update interest
                var existingCableInterestAR = _eventStore.Aggregates.Load<InterestAR>(cableToBeCut.WalkOfInterestId);

                var updateInterestResult = existingCableInterestAR.UpdateRouteNetworkElements(networkCmdContext, existingCableWalkAfterShrink.GetRouteNetworkInterest(cableToBeCut.WalkOfInterestId), interestsProjection, new WalkValidator(_routeNetworkRepository));

                if (updateInterestResult.IsFailed)
                    throw new ApplicationException($"Failed to update interest: {cableToBeCut.WalkOfInterestId} of span equipment: {cableToBeCut.Id} in RemoveSpanStructureFromSpanEquipmentCommandHandler Error: {updateInterestResult.Errors.First().Message}");

                ////////////////////////////
                // Create new cable

                var newCableWalkOfInterestId = Guid.NewGuid();
                              
                var newCableUtilityNetworkHops = GetUtilityHopsRelatedToWalk(cableToBeCut.UtilityNetworkHops, newCableWalk);

                var newCableEquipmentAR = new SpanEquipmentAR();

                var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

                var placeSpanEquipmentResult = newCableEquipmentAR.PlaceSpanEquipmentInUtilityNetwork(
                    cmdContext: commandContext,
                    spanEquipments,
                    spanEquipmentSpecifications,
                    Guid.NewGuid(),
                    cableToBeCut.SpecificationId,
                    newCableWalkOfInterestId,
                    newCableWalk.RouteNetworkElementRefs,
                    newCableUtilityNetworkHops,
                    cableToBeCut.ManufacturerId,
                    cableToBeCut.NamingInfo,
                    cableToBeCut.LifecycleInfo,
                    cableToBeCut.MarkingInfo,
                    cableToBeCut.AddressInfo
                );

                if (placeSpanEquipmentResult.IsFailed)
                    return Task.FromResult(Result.Fail(placeSpanEquipmentResult.Errors.First()));

                // create the interest
                var newCableInterestAR = new InterestAR();

                var interestProjection = _eventStore.Projections.Get<InterestsProjection>();

                OpenFTTH.RouteNetwork.Business.CommandContext routeNetworkCommandContext = new RouteNetwork.Business.CommandContext(commandContext.CorrelationId, commandContext.CmdId, commandContext.UserContext);

                var walkOfInterest = new RouteNetworkInterest(newCableWalkOfInterestId, RouteNetworkInterestKindEnum.WalkOfInterest, newCableWalk.RouteNetworkElementRefs);

                var registerWalkOfInterestResult = newCableInterestAR.RegisterWalkOfInterest(routeNetworkCommandContext, walkOfInterest, interestProjection, new WalkValidator(_routeNetworkRepository));

                if (registerWalkOfInterestResult.IsFailed)
                    throw new ApplicationException($"Failed to register walk of interest of span equipment in split in CutSpanSegmentsCommandHandler Error: {registerWalkOfInterestResult.Errors.First().Message}");

                var connects = CreateConnectedFromDisconnects(newCableEquipmentAR.SpanEquipment, disconnects);

                if (connects.Count > 0)
                {
                    // Connect the segments
                    var connectResult = newCableEquipmentAR.ConnectCableSpanSegmentsWithTerminals(
                           cmdContext: utilityCmdContext,
                           spanEquipmentSpecifications[newCableEquipmentAR.SpanEquipment.SpecificationId],
                           newCableWalk.ToNodeId,
                           connects.ToArray()
                    );

                    if (!connectResult.IsSuccess)
                    {
                        return Task.FromResult(connectResult);
                    }
                }

                _eventStore.Aggregates.Store(existingCableInterestAR);
                _eventStore.Aggregates.Store(existingCableEquipmentAR);

                _eventStore.Aggregates.Store(newCableInterestAR);
                _eventStore.Aggregates.Store(newCableEquipmentAR);

                NotifyExternalServicesAboutChange(cableToBeCut.Id, command.RouteNodeId);
            }

            return Task.FromResult(Result.Ok());
        }

        private UtilityNetworkHop[]? GetUtilityHopsRelatedToWalk(UtilityNetworkHop[]? utilityNetworkHops, ValidatedRouteNetworkWalk walk)
        {
            List<UtilityNetworkHop> newHopList = new List<UtilityNetworkHop>();

            foreach (var existingHop in utilityNetworkHops)
            {
                int relCount = 0;

                if (walk.NodeIds.Contains(existingHop.FromNodeId))
                    relCount++;

                if (walk.NodeIds.Contains(existingHop.ToNodeId))
                    relCount++;

                if (relCount == 2)
                    newHopList.Add(existingHop);
            }

            return newHopList.ToArray();
        }

        private bool CheckIfUtilityHopsIsCutInWalk(UtilityNetworkHop[]? utilityNetworkHops, Guid splitNode, ValidatedRouteNetworkWalk walk1, ValidatedRouteNetworkWalk walk2)
        {
            List<UtilityNetworkHop> newHopList = new List<UtilityNetworkHop>();

            foreach (var existingHop in utilityNetworkHops)
            {
                int walk1RelCount = 0;

                if (existingHop.FromNodeId != splitNode && walk1.NodeIds.Contains(existingHop.FromNodeId))
                    walk1RelCount++;

                if (existingHop.ToNodeId != splitNode && walk1.NodeIds.Contains(existingHop.ToNodeId))
                    walk1RelCount++;

                int walk2RelCount = 0;

                if (existingHop.FromNodeId != splitNode && walk2.NodeIds.Contains(existingHop.FromNodeId))
                    walk2RelCount++;

                if (existingHop.ToNodeId != splitNode && walk2.NodeIds.Contains(existingHop.ToNodeId))
                    walk2RelCount++;


                if (walk1RelCount > 0 && walk2RelCount > 0)
                    return true;
            }

            return false;
        }

        private static ValidatedRouteNetworkWalk GetWalkAfterNode(RouteNetworkElementIdList existingWalk, Guid routeNodeId)
        {
            RouteNetworkElementIdList newWalk = new RouteNetworkElementIdList();

            bool midNodeFound = false;

            foreach (var existingRef in existingWalk)
            {
                if (existingRef == routeNodeId)
                    midNodeFound = true;

                if (midNodeFound)
                    newWalk.Add(existingRef);

            }

            return new ValidatedRouteNetworkWalk(newWalk);
        }

        private static ValidatedRouteNetworkWalk GetWalkBeforeNode(RouteNetworkElementIdList existingWalk, Guid routeNodeId)
        {
            RouteNetworkElementIdList newWalk = new RouteNetworkElementIdList();

            bool midNodeFound = false;

            foreach (var existingRef in existingWalk)
            {
                if (!midNodeFound)
                    newWalk.Add(existingRef);

                if (existingRef == routeNodeId)
                    midNodeFound = true;
            }

            return new ValidatedRouteNetworkWalk(newWalk);
        }

        private Dictionary<int, DisconnectSpanSegmentFromTerminalOperation> FindCableEndDisconnects(UtilityNetworkProjection utilityNetwork, SpanEquipment cableToBeCut)
        {
            Dictionary<int, DisconnectSpanSegmentFromTerminalOperation> disconnects = new();

            foreach (var spanStructures in cableToBeCut.SpanStructures)
            {
                var spanSegment = spanStructures.SpanSegments.First();

                if (spanSegment.ToTerminalId != Guid.Empty)
                {
                    disconnects.Add(spanStructures.Position, new DisconnectSpanSegmentFromTerminalOperation(spanSegment.Id, spanSegment.ToTerminalId));
                }
            }

          

            return disconnects;
        }

        private List<SpanSegmentToSimpleTerminalConnectInfo> CreateConnectedFromDisconnects(SpanEquipment spanEquipment, Dictionary<int, DisconnectSpanSegmentFromTerminalOperation> disconnects)
        {
            List<SpanSegmentToSimpleTerminalConnectInfo> connects = new();

            foreach (var disconnect in disconnects)
            {
                var spanStructure = spanEquipment.SpanStructures.First(s => s.Level == 2 && s.Position == disconnect.Key);

                var connect = new SpanSegmentToSimpleTerminalConnectInfo(spanStructure.SpanSegments.First().Id, disconnect.Value.TerminalId);
                connect.ConnectionDirection = SpanSegmentToTerminalConnectionDirection.FromSpanSegmentToTerminal;
                connects.Add(connect);
            }

            return connects;
        }


        private Task<Result> CutConduits(CutSpanSegmentsAtRouteNode command, UtilityNetworkProjection utilityNetwork, InterestsProjection interestsProjection, IUtilityGraphSegmentRef spanSegmentGraphElement)
        {
            foreach (var spanSegmentToCut in command.SpanSegmentsToCut)
            {
                if (utilityNetwork.CheckIfConduitSegmentContainsCables(spanSegmentToCut))
                    return Task.FromResult(Result.Fail(new CutSpanSegmentsAtRouteNodeError(CutSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_CONTAIN_CABLE, $"The span segment id: {spanSegmentToCut} contain a cable. Cannot be cut.")));
            }

            var spanEquipment = spanSegmentGraphElement.SpanEquipment(utilityNetwork);
            var firstSpanSegment = spanSegmentGraphElement.SpanSegment(utilityNetwork);

            // If conduit containing only one span structure, then cut the whole span equipment
            if (spanEquipment.SpanStructures.Length == 1 && spanEquipment.SpanStructures[0].SpanSegments.Length == 1)
            {
                return CutConduitEquipment(command, utilityNetwork, interestsProjection, spanEquipment);
            }
            else
            {
                return CutConduitSpanSegments(command, spanEquipment);
            }
        }

        private Task<Result> CutConduitSpanSegments(CutSpanSegmentsAtRouteNode command, SpanEquipment spanEquipment)
        {
            // Get walk of interest of the span equipment
            var interestQueryResult = _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
                new GetRouteNetworkDetails(new InterestIdList() { spanEquipment.WalkOfInterestId })
            ).Result;

            if (interestQueryResult.IsFailed)
                return Task.FromResult(Result.Fail(new CutSpanSegmentsAtRouteNodeError(CutSpanSegmentsAtRouteNodeErrorCodes.FAILED_TO_GET_SPAN_EQUIPMENT_WALK_OF_INTEREST_INFORMATION, $"Got error trying to query interest information belonging to span equipment with id: {spanEquipment.Id} Error Message: {interestQueryResult.Errors.First().Message}")));

            if (interestQueryResult.Value is null || interestQueryResult.Value.Interests is null)
                throw new ApplicationException($"Got nothing back trying to query interest information belonging to span equipment with id: {spanEquipment.Id} Null was returned.");

            var spanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(spanEquipment.Id);

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            var cuteSpanEquipmentsResult = spanEquipmentAR.CutSpanSegments(
                cmdContext: commandContext,
                spanEquipmentWalkOfInterest: interestQueryResult.Value.Interests.First(),
                routeNodeId: command.RouteNodeId,
                spanSegmentsToCut: command.SpanSegmentsToCut
            );

            if (cuteSpanEquipmentsResult.IsSuccess)
            {
                _eventStore.Aggregates.Store(spanEquipmentAR);
                NotifyExternalServicesAboutChange(spanEquipment.Id, command.RouteNodeId);
            }

            return Task.FromResult(cuteSpanEquipmentsResult);
        }

        private Task<Result> CutConduitEquipment(CutSpanSegmentsAtRouteNode command, UtilityNetworkProjection utilityNetwork, InterestsProjection interestsProjection, SpanEquipment spanEquipment)
        {
            var spanEquipments = _eventStore.Projections.Get<UtilityNetworkProjection>().SpanEquipmentsByEquipmentId;
            var spanEquipmentSpecifications = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;

            // Check that only one conduit is selected
            if (command.SpanSegmentsToCut.Length > 1)
            {
                return Task.FromResult(Result.Fail(new CutSpanSegmentsAtRouteNodeError(CutSpanSegmentsAtRouteNodeErrorCodes.EXPECTED_ONLY_ONE_CONDUIT, $"Expected only one whole conduit to be cut. Cutting multiple conduits entirely at once is not allowed.")));
            }

            utilityNetwork.TryGetEquipment<SpanEquipment>(command.SpanSegmentsToCut[0], out var conduitToBeCut);

            if (IsAffixedToNodeContainer(conduitToBeCut))
            {
                return Task.FromResult(Result.Fail(new CutSpanSegmentsAtRouteNodeError(CutSpanSegmentsAtRouteNodeErrorCodes.CUTTING_CONDUIT_AFFIXED_TO_CONTAINER_NOT_ALLOWED, $"Cutting conduits affixed to a container is not allowed")));
            }

            var utilityCmdContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);
            var networkCmdContext = new RouteNetwork.Business.CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            var existingWalk = GetInterestInformation(conduitToBeCut);

            // Only cut cables not starting or ending in the node (where to cut)
            if (existingWalk.NodeIds.Contains(command.RouteNodeId) && existingWalk.NodeIds.First() != command.RouteNodeId && existingWalk.NodeIds.Last() != command.RouteNodeId)
            {

                // Disconnect eventually to terminals
                var disconnects = FindCableEndDisconnects(utilityNetwork, conduitToBeCut);

                var existingConduitEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(conduitToBeCut.Id);

                if (disconnects.Count > 0)
                {
                    // Disconnect the segments
                    var disconnectResult = existingConduitEquipmentAR.DisconnectSegmentsFromTerminals(
                           cmdContext: utilityCmdContext,
                           disconnects.Values.ToArray()
                    );

                    if (!disconnectResult.IsSuccess)
                    {
                        return Task.FromResult(disconnectResult);
                    }
                }

                ////////////////////////////
                // Shrink existing conduit

                var existingConduitWalkAfterShrink = GetWalkBeforeNode(existingWalk.RouteNetworkElementRefs, command.RouteNodeId);

                var newCableWalk = GetWalkAfterNode(existingWalk.RouteNetworkElementRefs, command.RouteNodeId);


                // Shrink conduit and update update utility hops
                var moveSpanEquipmentResult = existingConduitEquipmentAR.Shrink(utilityCmdContext, existingConduitWalkAfterShrink, existingWalk, null);

                if (moveSpanEquipmentResult.IsFailed)
                    return Task.FromResult(Result.Fail(moveSpanEquipmentResult.Errors.First()));

                // Update interest
                var existingConduitInterestAR = _eventStore.Aggregates.Load<InterestAR>(conduitToBeCut.WalkOfInterestId);

                var updateInterestResult = existingConduitInterestAR.UpdateRouteNetworkElements(networkCmdContext, existingConduitWalkAfterShrink.GetRouteNetworkInterest(conduitToBeCut.WalkOfInterestId), interestsProjection, new WalkValidator(_routeNetworkRepository));

                if (updateInterestResult.IsFailed)
                    throw new ApplicationException($"Failed to update interest: {conduitToBeCut.WalkOfInterestId} of span equipment: {conduitToBeCut.Id} in RemoveSpanStructureFromSpanEquipmentCommandHandler Error: {updateInterestResult.Errors.First().Message}");

                ////////////////////////////
                // Create new conduit

                var newConduitWalkOfInterestId = Guid.NewGuid();

                var newConduitEquipmentAR = new SpanEquipmentAR();

                var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

                var placeSpanEquipmentResult = newConduitEquipmentAR.PlaceSpanEquipmentInUtilityNetwork(
                    cmdContext: commandContext,
                    spanEquipments,
                    spanEquipmentSpecifications,
                    Guid.NewGuid(),
                    conduitToBeCut.SpecificationId,
                    newConduitWalkOfInterestId,
                    newCableWalk.RouteNetworkElementRefs,
                    null,
                    conduitToBeCut.ManufacturerId,
                    conduitToBeCut.NamingInfo,
                    conduitToBeCut.LifecycleInfo,
                    conduitToBeCut.MarkingInfo,
                    conduitToBeCut.AddressInfo
                );

                if (placeSpanEquipmentResult.IsFailed)
                    return Task.FromResult(Result.Fail(placeSpanEquipmentResult.Errors.First()));

                // create the interest
                var newCableInterestAR = new InterestAR();

                var interestProjection = _eventStore.Projections.Get<InterestsProjection>();

                OpenFTTH.RouteNetwork.Business.CommandContext routeNetworkCommandContext = new RouteNetwork.Business.CommandContext(commandContext.CorrelationId, commandContext.CmdId, commandContext.UserContext);

                var walkOfInterest = new RouteNetworkInterest(newConduitWalkOfInterestId, RouteNetworkInterestKindEnum.WalkOfInterest, newCableWalk.RouteNetworkElementRefs);

                var registerWalkOfInterestResult = newCableInterestAR.RegisterWalkOfInterest(routeNetworkCommandContext, walkOfInterest, interestProjection, new WalkValidator(_routeNetworkRepository));

                if (registerWalkOfInterestResult.IsFailed)
                    throw new ApplicationException($"Failed to register walk of interest of span equipment in split in CutSpanSegmentsCommandHandler Error: {registerWalkOfInterestResult.Errors.First().Message}");

                var connects = CreateConnectedFromDisconnects(newConduitEquipmentAR.SpanEquipment, disconnects);

                if (connects.Count > 0)
                {
                    // Connect the segments
                    var connectResult = newConduitEquipmentAR.ConnectCableSpanSegmentsWithTerminals(
                           cmdContext: utilityCmdContext,
                           spanEquipmentSpecifications[newConduitEquipmentAR.SpanEquipment.SpecificationId],
                           newCableWalk.ToNodeId,
                           connects.ToArray()
                    );

                    if (!connectResult.IsSuccess)
                    {
                        return Task.FromResult(connectResult);
                    }
                }

                _eventStore.Aggregates.Store(existingConduitInterestAR);
                _eventStore.Aggregates.Store(existingConduitEquipmentAR);

                _eventStore.Aggregates.Store(newCableInterestAR);
                _eventStore.Aggregates.Store(newConduitEquipmentAR);

                NotifyExternalServicesAboutChange(conduitToBeCut.Id, command.RouteNodeId);
            }

            return Task.FromResult(Result.Ok());
        }

        private bool IsAffixedToNodeContainer(SpanEquipment spanEquipment)
        {
            if (spanEquipment.NodeContainerAffixes == null)
                return false;

            foreach (var affix in spanEquipment.NodeContainerAffixes)
            {
                return true;
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

        private async void NotifyExternalServicesAboutChange(Guid spanEquipmentId, Guid routeNodeId)
        {
            List<IdChangeSet> idChangeSets = new List<IdChangeSet>
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
                    category: "EquipmentConnectivityModification.Cut",
                    idChangeSets: idChangeSets.ToArray(),
                    affectedRouteNetworkElementIds: new Guid[] { routeNodeId }
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }
    }
}
