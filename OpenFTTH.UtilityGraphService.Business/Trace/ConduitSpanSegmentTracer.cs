using OpenFTTH.Results;
using OpenFTTH.Address.API.Model;
using OpenFTTH.Address.API.Queries;
using OpenFTTH.CQRS;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.UtilityGraphService.API.Model.Trace;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using OpenFTTH.UtilityGraphService.Business.Graph.Trace;

namespace OpenFTTH.UtilityGraphService.Business.Trace
{
    /// <summary>
    /// Tracer optimized for tracing a single conduit span segments
    /// </summary>
    public class ConduitSpanSegmentTracer
    {
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly UtilityNetworkProjection _utilityNetwork;

        public ConduitSpanSegmentTracer(IQueryDispatcher queryDispatcher, UtilityNetworkProjection utilityNetwork)
        {
            _queryDispatcher = queryDispatcher;
            _utilityNetwork = utilityNetwork;
        }

        public ConduitSpanSegmentTracerResult? Trace(Guid spanSegmentIdToTrace)
        {
            var intermidiateTraceResult = GatherNetworkGraphTraceInformation(spanSegmentIdToTrace);

            if (intermidiateTraceResult.InterestList.Count > 0)
            {
                var routeNetworkInformation = GatherRouteNetworkInformation(_queryDispatcher, intermidiateTraceResult.InterestList);

                if (routeNetworkInformation.Interests == null)
                    throw new ApplicationException("Failed to query route network interest information. Interest information is null");


                // Find the segments covered by trace
                List<Guid> spanSegmentIds = new();
                foreach (var segmentHop in intermidiateTraceResult.SegmentWalk.Hops)
                {
                    var walkIds = routeNetworkInformation.Interests[segmentHop.WalkOfInterestId].RouteNetworkElementRefs;

                    spanSegmentIds.Add(segmentHop.SpanSegmentId);
                }

                // Create walk by folowing route segments
                var walk = CreateWalk(routeNetworkInformation, intermidiateTraceResult.SegmentWalk);


                // Create utility network trace (span segments)
                var utilityNetworkTrace = new UtilityNetworkTraceResult(
                        spanSegmentId: intermidiateTraceResult.SegmentWalk.SpanEquipmentOrSegmentId,
                        fromTerminalId: null,
                        toTerminalId: null,
                        spanSegmentIds: spanSegmentIds.ToArray(),
                        tags: intermidiateTraceResult.SegmentWalk.Tags
                  );

                return new ConduitSpanSegmentTracerResult(walk, utilityNetworkTrace);
            }
            else
                return null;
        }

        private RouteNetworkElementIdList CreateWalk(GetRouteNetworkDetailsResult routeNetworkInformation, SegmentWalk segmentWalk)
        {
            // Create the walk
            var walk = new RouteNetworkElementIdList();

            bool firstHop = true;

            foreach (var segmentHop in segmentWalk.Hops)
            {
                var walkIds = routeNetworkInformation.Interests[segmentHop.WalkOfInterestId].RouteNetworkElementRefs;

                // TODO: Refactor error handling on this one.
                try
                {
                    var routeElementsBetweenNodes = GetRouteElementsBetweenNodes(walkIds, segmentHop.FromNodeId, segmentHop.ToNodeId);

                    if (firstHop)
                        walk.Add(segmentHop.FromNodeId);

                    walk.AddRange(routeElementsBetweenNodes);

                    walk.Add(segmentHop.ToNodeId);
                }
                catch (ApplicationException ex)
                {
                    Log.Error($"Error collecting route segments between route node: {segmentHop.FromNodeId} and route node: {segmentHop.ToNodeId} in walk of interest: {segmentHop.WalkOfInterestId} while tracing span segment: {segmentWalk.SpanEquipmentOrSegmentId} Error: {ex.Message}");
                }

                firstHop = false;
            }

            return walk;
        }

        private List<Guid> GetRouteElementsBetweenNodes(RouteNetworkElementIdList walkIds, Guid startNodeId, Guid endNodeId)
        {
            List<Guid> result = new();

            var startNodeIndex = walkIds.IndexOf(startNodeId);
            var endNodeIndex = walkIds.IndexOf(endNodeId);

            if (startNodeIndex < 0 || endNodeIndex < 0)
                throw new ApplicationException($"Failed to find start node: {startNodeId} or end node {endNodeId} in walk.");

            if (startNodeIndex < endNodeIndex)
            {
                for (int i = startNodeIndex + 1; i < endNodeIndex; i += 1)
                {
                    result.Add(walkIds[i]);
                }
            }
            else
            {
                for (int i = startNodeIndex - 1; i > endNodeIndex; i -= 1)
                {
                    result.Add(walkIds[i]);
                }
            }

            return result;
        }

        private static GetRouteNetworkDetailsResult GatherRouteNetworkInformation(IQueryDispatcher queryDispatcher, IEnumerable<Guid> walkOfInterestIds)
        {
            InterestIdList interestIdList = new();
            interestIdList.AddRange(walkOfInterestIds);

            var interestQueryResult = queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
                new GetRouteNetworkDetails(interestIdList)
                {
                    RouteNetworkElementFilter = new RouteNetworkElementFilterOptions() { IncludeNamingInfo = false, IncludeCoordinates = false }
                }
            ).Result;

            if (interestQueryResult.IsFailed)
                throw new ApplicationException("Failed to query route network information. Got error: " + interestQueryResult.Errors.First().Message);

            return interestQueryResult.Value;
        }

        private IntermidiateTraceResult GatherNetworkGraphTraceInformation(Guid spanSegmentIdToTrace)
        {
            IntermidiateTraceResult result = new();

            var spanTraceResult = _utilityNetwork.Graph.SimpleTrace(spanSegmentIdToTrace);

            string[]? tags = ExtractTagsFromTrace(_utilityNetwork, spanTraceResult);

            // We're dealing with a connected segment if non-empty trace result is returned
            if (spanTraceResult.Upstream.Length > 0)
            {
                var segmentWalk = new SegmentWalk(spanSegmentIdToTrace, tags);

                for (int downstreamIndex = spanTraceResult.Downstream.Length - 1; downstreamIndex > 0; downstreamIndex--)
                {
                    var item = spanTraceResult.Downstream[downstreamIndex];

                    if (item is UtilityGraphConnectedSegment connectedSegment)
                    {
                        // Snatch walk of interest id
                        Guid walkOfInterestId = AddWalkOfInterestToResult(result, connectedSegment);

                        var segmentHop = new SegmentWalkHop(
                            spanEquipmentOrSegmentId: connectedSegment.Id,
                            fromNodeId: ((UtilityGraphConnectedTerminal)spanTraceResult.Downstream[downstreamIndex + 1]).RouteNodeId,
                            toNodeId: ((UtilityGraphConnectedTerminal)spanTraceResult.Downstream[downstreamIndex - 1]).RouteNodeId,
                            walkOfInterestId: walkOfInterestId,
                            addressInfo: ((UtilityGraphConnectedSegment)item).SpanEquipment(_utilityNetwork).AddressInfo
                        );

                        segmentWalk.Hops.Add(segmentHop);
                    }
                }

                for (int upstreamIndex = 0; upstreamIndex < spanTraceResult.Upstream.Length; upstreamIndex++)
                {
                    var item = spanTraceResult.Upstream[upstreamIndex];

                    if (item is UtilityGraphConnectedSegment connectedSegment)
                    {
                        // Snatch walk of interest id
                        var walkOfInterestId = AddWalkOfInterestToResult(result, connectedSegment);

                        if (upstreamIndex == 0)
                        {
                            var segmentHop = new SegmentWalkHop(
                                spanEquipmentOrSegmentId: connectedSegment.Id,
                                fromNodeId: ((UtilityGraphConnectedTerminal)spanTraceResult.Downstream[1]).RouteNodeId,
                                toNodeId: ((UtilityGraphConnectedTerminal)spanTraceResult.Upstream[upstreamIndex + 1]).RouteNodeId,
                                walkOfInterestId: walkOfInterestId,
                                addressInfo: ((UtilityGraphConnectedSegment)item).SpanEquipment(_utilityNetwork).AddressInfo
                            );

                            segmentWalk.Hops.Add(segmentHop);
                        }
                        else
                        {
                            var segmentHop = new SegmentWalkHop(
                                spanEquipmentOrSegmentId: connectedSegment.Id,
                                fromNodeId: ((UtilityGraphConnectedTerminal)spanTraceResult.Upstream[upstreamIndex - 1]).RouteNodeId,
                                toNodeId: ((UtilityGraphConnectedTerminal)spanTraceResult.Upstream[upstreamIndex + 1]).RouteNodeId,
                                walkOfInterestId: walkOfInterestId,
                                addressInfo: ((UtilityGraphConnectedSegment)item).SpanEquipment(_utilityNetwork).AddressInfo
                            );

                            segmentWalk.Hops.Add(segmentHop);
                        }
                    }
                }

                result.SegmentWalk = segmentWalk;
            }
            // We're dealing with an unconnected segment
            else
            {
                if (_utilityNetwork.Graph.TryGetGraphElement<UtilityGraphDisconnectedSegment>(spanSegmentIdToTrace, out var disconnectedSegment))
                {

                    var disconnectedSpanSegment = disconnectedSegment.SpanSegment(_utilityNetwork);
                    var spanEquipment = disconnectedSegment.SpanEquipment(_utilityNetwork);

                    var segmentHop = new SegmentWalkHop(
                        spanEquipmentOrSegmentId: disconnectedSpanSegment.Id,
                        fromNodeId: spanEquipment.NodesOfInterestIds[disconnectedSpanSegment.FromNodeOfInterestIndex],
                        toNodeId: spanEquipment.NodesOfInterestIds[disconnectedSpanSegment.ToNodeOfInterestIndex],
                        walkOfInterestId: spanEquipment.WalkOfInterestId,
                        addressInfo: spanEquipment.AddressInfo
                    );

                    var segmentWalk = new SegmentWalk(spanSegmentIdToTrace, tags);

                    segmentWalk.Hops.Add(segmentHop);
                    result.SegmentWalk = segmentWalk;

                    AddWalkOfInterestToResult(result, disconnectedSegment);
                }
            }

            return result;
        }

        public static string[]? ExtractTagsFromTrace(UtilityNetworkProjection utilityNetwork, UtilityGraphTraceResult spanTraceResult)
        {
            HashSet<string> tags = new HashSet<string>();

            foreach (var segment in spanTraceResult.AllIncludingSource)
            {
                if (segment is IUtilityGraphSegmentRef traceSegmentRef)
                {
                    var spanEquipmentSegmentTags = traceSegmentRef.SpanEquipment(utilityNetwork).EquipmentTags;

                    if (spanEquipmentSegmentTags != null)
                    {
                        foreach (var spanEquipmentSegmentTag in spanEquipmentSegmentTags)
                        {
                            if (spanEquipmentSegmentTag.TerminalOrSpanId == segment.Id && spanEquipmentSegmentTag.Tags != null)
                            {
                                foreach (var spanSegmentTag in spanEquipmentSegmentTag.Tags)
                                {
                                    if (!tags.Contains(spanSegmentTag))
                                        tags.Add(spanSegmentTag);
                                }
                            }
                        }
                    }
                }

                if (segment is IUtilityGraphTerminalRef traceTerminalRef)
                {
                    if (traceTerminalRef.TerminalEquipmentId != Guid.Empty)
                    {
                        var terminalEquipmentSegmentTags = traceTerminalRef.TerminalEquipment(utilityNetwork).EquipmentTags;

                        if (terminalEquipmentSegmentTags != null)
                        {
                            foreach (var terminalEquipmentTag in terminalEquipmentSegmentTags)
                            {
                                if (terminalEquipmentTag.TerminalOrSpanId == segment.Id && terminalEquipmentTag.Tags != null)
                                {
                                    foreach (var terminalTag in terminalEquipmentTag.Tags)
                                    {
                                        if (!tags.Contains(terminalTag))
                                            tags.Add(terminalTag);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (tags.Count == 0)
                return null;
            else
                return tags.ToArray();
        }

        public static string[]? ExtractTagsFromSpanEquipment(SpanEquipment spanEquipment)
        {
            HashSet<string> tags = new HashSet<string>();

            var spanEquipmentSegmentTags = spanEquipment.EquipmentTags;

            if (spanEquipmentSegmentTags != null)
            {
                foreach (var spanEquipmentSegmentTag in spanEquipmentSegmentTags)
                {
                    if (spanEquipmentSegmentTag.Tags != null)
                    {
                        foreach (var spanSegmentTag in spanEquipmentSegmentTag.Tags)
                        {
                            if (!tags.Contains(spanSegmentTag))
                                tags.Add(spanSegmentTag);
                        }
                    }
                }
            }

            if (tags.Count == 0)
                return null;
            else
                return tags.ToArray();
        }

        private Guid AddWalkOfInterestToResult(IntermidiateTraceResult result, IUtilityGraphSegmentRef segment)
        {
            var walkOfInterestId = segment.SpanEquipment(_utilityNetwork).WalkOfInterestId;

            if (!result.InterestList.Contains(walkOfInterestId))
                result.InterestList.Add(walkOfInterestId);
            return walkOfInterestId;
        }

        private class IntermidiateTraceResult
        {
            public HashSet<Guid> InterestList = new();
            public SegmentWalk SegmentWalk;
        }
    }
}
