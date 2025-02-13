using DAX.EventProcessing;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Changes;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.Trace;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using OpenFTTH.UtilityGraphService.Business.Trace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenFTTH.RouteNetwork.Business.Interest;
using OpenFTTH.RouteNetwork.Business.RouteElements.StateHandling;
using OpenFTTH.RouteNetwork.Business.Interest.Projections;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class PlaceSpanEquipmentInUtilityNetworkCommandHandler : ICommandHandler<PlaceSpanEquipmentInUtilityNetwork, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly IExternalEventProducer _externalEventProducer;
        private readonly UtilityNetworkProjection _utilityNetwork;
        private readonly IRouteNetworkRepository _routeNetworkRepository;

        public PlaceSpanEquipmentInUtilityNetworkCommandHandler(IEventStore eventStore,IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer, IRouteNetworkRepository routeNodeRepository)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = externalEventProducer;
            _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
            _routeNetworkRepository = routeNodeRepository;

        }

        public Task<Result> HandleAsync(PlaceSpanEquipmentInUtilityNetwork command)
        {
            if (command.RoutingHops == null || command.RoutingHops.Length == 0)
            {
                return Task.FromResult(Result.Fail(new PlaceSpanEquipmentInUtilityNetworkError(PlaceSpanEquipmentInUtilityNetworkErrorCodes.ROUTING_HOPS_CANNOT_BE_NULL_OR_EMPTY, $"One or more routing hops must be specified")));
            }

            var traceRoutingHopsResult = ProcessRoutingHops(command);

            if (traceRoutingHopsResult.IsFailed)
                return Task.FromResult(Result.Fail(traceRoutingHopsResult.Errors.First()));

            if (traceRoutingHopsResult.Value.ValidatedRouteNetworkWalk == null)
                throw new ApplicationException("ProcessRoutingHops return IsSuccess and null value. Please check code.");

            var walkOfInterestId = Guid.NewGuid();

            var spanEquipments = _eventStore.Projections.Get<UtilityNetworkProjection>().SpanEquipmentsByEquipmentId;
            var spanEquipmentSpecifications = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;

            var spanEquipmentAR = new SpanEquipmentAR();

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            var placeSpanEquipmentResult = spanEquipmentAR.PlaceSpanEquipmentInUtilityNetwork(
                cmdContext: commandContext,
                spanEquipments,
                spanEquipmentSpecifications,
                command.SpanEquipmentId,
                command.SpanEquipmentSpecificationId,
                walkOfInterestId,
                traceRoutingHopsResult.Value.ValidatedRouteNetworkWalk.RouteNetworkElementRefs,
                traceRoutingHopsResult.Value.UtilityHops.ToArray(),
                command.ManufacturerId,
                command.NamingInfo,
                command.LifecycleInfo,
                command.MarkingInfo,
                command.AddressInfo
            );

            if (placeSpanEquipmentResult.IsFailed)
                return Task.FromResult(Result.Fail(placeSpanEquipmentResult.Errors.First()));

            var interestAR = new InterestAR();

            var interestProjection = _eventStore.Projections.Get<InterestsProjection>();

            OpenFTTH.RouteNetwork.Business.CommandContext routeNetworkCommandContext = new RouteNetwork.Business.CommandContext(commandContext.CorrelationId, commandContext.CmdId, commandContext.UserContext);

            var walkOfInterest = new RouteNetworkInterest(walkOfInterestId, RouteNetworkInterestKindEnum.WalkOfInterest, traceRoutingHopsResult.Value.ValidatedRouteNetworkWalk.RouteNetworkElementRefs);

            var registerWalkOfInterestResult = interestAR.RegisterWalkOfInterest(routeNetworkCommandContext, walkOfInterest, interestProjection, new WalkValidator(_routeNetworkRepository));

            if (registerWalkOfInterestResult.IsFailed)
                throw new ApplicationException($"Failed to register walk of interest of span equipment: {command.SpanEquipmentId} in PlaceSpanEquipmentInUtilityNetworkCommandHandler Error: {registerWalkOfInterestResult.Errors.First().Message}");

            _eventStore.Aggregates.Store(spanEquipmentAR);
            _eventStore.Aggregates.Store(interestAR);

            NotifyExternalServicesAboutChange(command.SpanEquipmentId, traceRoutingHopsResult.Value.ValidatedRouteNetworkWalk.RouteNetworkElementRefs.ToArray());

            return Task.FromResult(Result.Ok());
        }

        private Result<ProcessRoutingHopsResult> ProcessRoutingHops(PlaceSpanEquipmentInUtilityNetwork command)
        {
            // Trace all hops
            var traceAllHopsResult = TraceAllHops(command);

            if (traceAllHopsResult.IsFailed)
                return Result.Fail(traceAllHopsResult.Errors.First());

            var hopResults = traceAllHopsResult.Value;


            // Make sure walks can be connected together
            var checkAndReverseSpanSegmentTracesResult = CheckAndReverseSpanSegmentTraces(hopResults);

            if (checkAndReverseSpanSegmentTracesResult.IsFailed)
                return Result.Fail(checkAndReverseSpanSegmentTracesResult.Errors.First());


            // Connect walks together
            RouteNetworkElementIdList routeNeworkElements = new RouteNetworkElementIdList();

            routeNeworkElements.AddRange(hopResults[0].ValidatedRouteNetworkWalk.RouteNetworkElementRefs);

            for (int subWalkIndex = 1; subWalkIndex < hopResults.Count; subWalkIndex++)
            {
                for (int routeNetworkElementIndex = 1; routeNetworkElementIndex < hopResults[subWalkIndex].ValidatedRouteNetworkWalk.RouteNetworkElementRefs.Count; routeNetworkElementIndex++)
                {
                    routeNeworkElements.Add(hopResults[subWalkIndex].ValidatedRouteNetworkWalk.RouteNetworkElementRefs[routeNetworkElementIndex]);
                }
            }

            // Create utility hops
            List<UtilityNetworkHop> utilityNetworkHops = new();

            foreach (var hopResult in hopResults)
            {
                if (hopResult.UtilityNetworkTrace != null)
                {
                    List<SpanEquipmentSpanEquipmentAffix> affixes = new();

                    foreach (var segmentId in hopResult.UtilityNetworkTrace.SpanSegmentIds)
                    {
                        SpanEquipmentAffixDirectionEnum direction = hopResult.IsReversed ? SpanEquipmentAffixDirectionEnum.Backward : SpanEquipmentAffixDirectionEnum.Forward;

                        affixes.Add(new SpanEquipmentSpanEquipmentAffix(segmentId, direction));
                    }

                    //hopResult.UtilityNetworkTrace.

                    utilityNetworkHops.Add(new UtilityNetworkHop(hopResult.ValidatedRouteNetworkWalk.FromNodeId, hopResult.ValidatedRouteNetworkWalk.ToNodeId, affixes.ToArray()));
                }
            }

            return Result.Ok(
                new ProcessRoutingHopsResult(new ValidatedRouteNetworkWalk(routeNeworkElements), utilityNetworkHops)
            );
        }

        private Result CheckAndReverseSpanSegmentTraces(List<ProcessedHopResult> segmentTraceResults)
        {
            bool first = true;
            ValidatedRouteNetworkWalk? prevSubWalk = null;
            int hopNumber = 1;

            for (int i = 0; i < segmentTraceResults.Count; i++)
            {
                var currentSubWalk = segmentTraceResults[i].ValidatedRouteNetworkWalk;

                if (!first)
                {
                    if (prevSubWalk == null)
                        throw new ApplicationException("Expected prebSubWalk to be non-null. Please check code");

                    if (currentSubWalk.FromNodeId == prevSubWalk.ToNodeId)
                    {
                        // Everything perfect, we need not to reverse any sub walks
                    }
                    else if (currentSubWalk.ToNodeId == prevSubWalk.ToNodeId)
                    {
                        // We reverse the current one
                        segmentTraceResults[i].Reverse();
                    }
                    else if (currentSubWalk.FromNodeId == prevSubWalk.FromNodeId)
                    {
                        // We reverse the prev one
                        segmentTraceResults[i - 1].Reverse();
                    }
                    else if (currentSubWalk.ToNodeId == prevSubWalk.FromNodeId)
                    {
                        // We reverse both
                        segmentTraceResults[i].Reverse();
                        segmentTraceResults[i - 1].Reverse();
                    }
                    else
                    {
                        return Result.Fail(new PlaceSpanEquipmentInUtilityNetworkError(PlaceSpanEquipmentInUtilityNetworkErrorCodes.ERROR_CALCULATING_WALK, $"Cannot connect walk of hop number: {hopNumber - 1} ({prevSubWalk.FromNodeId}->{prevSubWalk.ToNodeId}) to walk of hop number: {hopNumber} ({currentSubWalk.FromNodeId}->{currentSubWalk.ToNodeId}). Are you sure the walks/traces of the routing hops specified are adjacent?"));
                    }
                }

                first = false;
                prevSubWalk = currentSubWalk;
                hopNumber++;
            }

            return Result.Ok();
        }

        private Result<List<ProcessedHopResult>> TraceAllHops(PlaceSpanEquipmentInUtilityNetwork command)
        {
            List<ProcessedHopResult> processedHopsResult = new();

            // Find walks for all hops
            foreach (var routingHop in command.RoutingHops)
            {
                // Route by span segment id
                if (routingHop.Kind == RoutingHopKind.RouteThroughSpanEquipmentBySpanSegmentId)
                {
                    if (routingHop.StartSpanSegmentId == null)
                        return Result.Fail(new PlaceSpanEquipmentInUtilityNetworkError(PlaceSpanEquipmentInUtilityNetworkErrorCodes.INVALID_ROUTE_NETWORK_HOP, $"RoutingHopKind.RouteThroughSpanEquipmentBySpanSegmentId hop must have a non-null StartSpanSegmentId"));

                    var spanSegmentId = routingHop.StartSpanSegmentId.Value;

                    var tracedHopResult = TraceSpanSegment(spanSegmentId);

                    if (tracedHopResult.IsFailed)
                        return Result.Fail(tracedHopResult.Errors.First());

                    if (tracedHopResult.Value.ValidatedRouteNetworkWalk.FromNodeId == routingHop.StartRouteNode)
                    {
                        // do nothing, the segment trace direction is ok
                    }
                    else if (tracedHopResult.Value.ValidatedRouteNetworkWalk.ToNodeId == routingHop.StartRouteNode)
                    {
                        tracedHopResult.Value.Reverse();
                    }
                    else
                    {
                        return Result.Fail(new PlaceSpanEquipmentInUtilityNetworkError(PlaceSpanEquipmentInUtilityNetworkErrorCodes.SPAN_SEGMENT_NOT_RELATED_TO_ROUTE_NODE, $"The span segment: {tracedHopResult.Value.SegmentId} do not start or end in route node: {routingHop.StartRouteNode}"));
                    }

                    processedHopsResult.Add(tracedHopResult.Value);
                }
                // Route by span equipment id and structure index
                else if (routingHop.Kind == RoutingHopKind.RouteThroughSpanEquipmentByStructureIndex)
                {
                    if (routingHop.StartRouteNode == null)
                        return Result.Fail(new PlaceSpanEquipmentInUtilityNetworkError(PlaceSpanEquipmentInUtilityNetworkErrorCodes.INVALID_ROUTE_NETWORK_HOP, $"RoutingHopKind.RouteThroughSpanEquipmentByStructureIndex hop must have a non-null StartRouteNode"));

                    if (routingHop.StartSpanEquipmentId == null)
                        return Result.Fail(new PlaceSpanEquipmentInUtilityNetworkError(PlaceSpanEquipmentInUtilityNetworkErrorCodes.INVALID_ROUTE_NETWORK_HOP, $"RoutingHopKind.RouteThroughSpanEquipmentByStructureIndex hop must have a non-null StartSpanEquipmentId"));

                    if (routingHop.StartStrutureIndex == null)
                        return Result.Fail(new PlaceSpanEquipmentInUtilityNetworkError(PlaceSpanEquipmentInUtilityNetworkErrorCodes.INVALID_ROUTE_NETWORK_HOP, $"RoutingHopKind.RouteThroughSpanEquipmentByStructureIndex hop must have a non-null StartStrutureIndex"));

                    // Try find the span segment id from provided hop info
                    var findSegmentIdResult = FindSpanSegmentId(routingHop.StartRouteNode.Value, routingHop.StartSpanEquipmentId.Value, routingHop.StartStrutureIndex.Value);

                    if (findSegmentIdResult.IsFailed)
                        return Result.Fail(findSegmentIdResult.Errors.First());

                    // Trace the hop
                    var tracedHopResult = TraceSpanSegment(findSegmentIdResult.Value);

                    if (tracedHopResult.IsFailed)
                        return Result.Fail(tracedHopResult.Errors.First());

                    processedHopsResult.Add(tracedHopResult.Value);
                }

                // Route through route network
                else if (routingHop.Kind == RoutingHopKind.RouteThroughRouteNetwork)
                {
                    if (routingHop.WalkOfinterest == null)
                        return Result.Fail(new PlaceSpanEquipmentInUtilityNetworkError(PlaceSpanEquipmentInUtilityNetworkErrorCodes.INVALID_ROUTE_NETWORK_HOP, $"RoutingHopKind.RouteThroughRouteNetwork hop must have a non-null WalkOfInterest"));

                    var walk = new RouteNetworkElementIdList();
                    walk.AddRange(routingHop.WalkOfinterest);


                    var walkValidator = new WalkValidator(_routeNetworkRepository);

                    var validateInterestResult = walkValidator.ValidateWalk(walk);

                    if (validateInterestResult.IsFailed)
                        return Result.Fail(validateInterestResult.Errors.First());

                    if (validateInterestResult.IsFailed)
                        return Result.Fail(validateInterestResult.Errors.First());

                    processedHopsResult.Add(new ProcessedHopResult(null, new ValidatedRouteNetworkWalk(validateInterestResult.Value), null));
                }
            }

            return Result.Ok(processedHopsResult);
        }

        private Result<Guid> FindSpanSegmentId(Guid routeNodeId, Guid spanEquipmentId, int structureIndex)
        {
            if (!_utilityNetwork.TryGetEquipment<SpanEquipment>(spanEquipmentId, out var spanEquipment))
                return Result.Fail(new PlaceSpanEquipmentInUtilityNetworkError(PlaceSpanEquipmentInUtilityNetworkErrorCodes.INVALID_SPAN_EQUIPMENT_ID_NOT_FOUND, $"Cannot find any span equipment in the utility graph with id: {spanEquipmentId}"));

            if (structureIndex < 0 || structureIndex >= spanEquipment.SpanStructures.Length)
                return Result.Fail(new PlaceSpanEquipmentInUtilityNetworkError(PlaceSpanEquipmentInUtilityNetworkErrorCodes.INVALID_SPAN_EQUIPMENT_STRUCTURE_INDEX_OUT_OF_BOUNDS, $"Cannot lookup structure in span equipment {spanEquipmentId} at index: {structureIndex}. The span equipment has {spanEquipment.SpanStructures.Length} structures."));

            var spanStructure = spanEquipment.SpanStructures[structureIndex];

            foreach (var spanSegment in spanStructure.SpanSegments)
            {
                var fromNode = spanEquipment.NodesOfInterestIds[spanSegment.FromNodeOfInterestIndex];

                if (fromNode == routeNodeId)
                    return Result.Ok(spanSegment.Id);

                var toNode = spanEquipment.NodesOfInterestIds[spanSegment.ToNodeOfInterestIndex];

                if (toNode == routeNodeId)
                    return Result.Ok(spanSegment.Id);
            }

            return Result.Fail(new PlaceSpanEquipmentInUtilityNetworkError(PlaceSpanEquipmentInUtilityNetworkErrorCodes.SPAN_SEGMENT_NOT_RELATED_TO_ROUTE_NODE, $"The span segment: {spanEquipment} in span equipment {spanEquipmentId} do not start or end in route node: {routeNodeId}"));
        }

        private Result<ProcessedHopResult> TraceSpanSegment(Guid spanSegmentIdToTrace)
        {
            if (!_utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(spanSegmentIdToTrace, out var spanSegmentGraphElement))
                return Result.Fail(new PlaceSpanEquipmentInUtilityNetworkError(PlaceSpanEquipmentInUtilityNetworkErrorCodes.INVALID_SPAN_SEGMENT_ID, $"Cannot find any span segment in the utility graph with id: {spanSegmentIdToTrace}"));

            var spanEquipment = spanSegmentGraphElement.SpanEquipment(_utilityNetwork);

            var traceBuilder = new ConduitSpanSegmentTracer(_queryDispatcher, _utilityNetwork);

            var traceInfo = traceBuilder.Trace(spanSegmentIdToTrace);

            if (traceInfo == null || traceInfo.RouteNetworkWalk == null)
            {
                return Result.Fail(new PlaceSpanEquipmentInUtilityNetworkError(PlaceSpanEquipmentInUtilityNetworkErrorCodes.ERROR_TRACING_SPAN_SEGMENT, $"Error tracing span segment with id: {spanSegmentIdToTrace} in span equipment with id: {spanEquipment.Id}. Expected 1 route network walk, but got none."));
            }

            if (traceInfo == null || traceInfo.UtilityNetworkTrace == null)
            {
                return Result.Fail(new PlaceSpanEquipmentInUtilityNetworkError(PlaceSpanEquipmentInUtilityNetworkErrorCodes.ERROR_TRACING_SPAN_SEGMENT, $"Error tracing span segment with id: {spanSegmentIdToTrace} in span equipment with id: {spanEquipment.Id}. Expected 1 utility network trace result, got got none."));
            }

            var walk = new RouteNetworkElementIdList();
            walk.AddRange(traceInfo.RouteNetworkWalk);

            var walkValidator = new WalkValidator(_routeNetworkRepository);

            var validateInterestResult = walkValidator.ValidateWalk(walk);

            if (validateInterestResult.IsFailed)
                return Result.Fail(validateInterestResult.Errors.First());

            return Result.Ok(
                new ProcessedHopResult(spanSegmentIdToTrace, new ValidatedRouteNetworkWalk(validateInterestResult.Value), traceInfo.UtilityNetworkTrace)
            );
        }

        private async void NotifyExternalServicesAboutChange(Guid spanEquipmentId, Guid[] affectedRouteNetworkElementIds)
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
                    category: "EquipmentModification",
                    idChangeSets: idChangeSets.ToArray(),
                    affectedRouteNetworkElementIds: affectedRouteNetworkElementIds
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }

        class ProcessRoutingHopsResult
        {
            public ValidatedRouteNetworkWalk ValidatedRouteNetworkWalk { get; set; }

            public UtilityNetworkHop[] UtilityHops { get; set; }

            public ProcessRoutingHopsResult(ValidatedRouteNetworkWalk validatedRouteNetworkWalk, List<UtilityNetworkHop> utilityHops)
            {
                ValidatedRouteNetworkWalk = validatedRouteNetworkWalk;
                this.UtilityHops = utilityHops.ToArray();
            }
        }

        class ProcessedHopResult
        {
            public ValidatedRouteNetworkWalk ValidatedRouteNetworkWalk { get; protected set; }
            public bool IsReversed { get; protected set; }
            public UtilityNetworkTraceResult? UtilityNetworkTrace { get; protected set; }
            public Guid? SegmentId { get; }

            public ProcessedHopResult(Guid? segmentId, ValidatedRouteNetworkWalk validatedRouteNetworkWalk, UtilityNetworkTraceResult? utilityNetworkTrace)
            {
                SegmentId = segmentId;
                ValidatedRouteNetworkWalk = validatedRouteNetworkWalk;
                UtilityNetworkTrace = utilityNetworkTrace;
            }

            public void Reverse()
            {
                if (ValidatedRouteNetworkWalk != null)
                {
                    ValidatedRouteNetworkWalk = ValidatedRouteNetworkWalk.Reverse();
                }

                if (UtilityNetworkTrace != null)
                {
                    UtilityNetworkTrace = UtilityNetworkTrace.Reverse();
                }

                IsReversed = IsReversed == false ? true : false;
            }
        }

    }


}
