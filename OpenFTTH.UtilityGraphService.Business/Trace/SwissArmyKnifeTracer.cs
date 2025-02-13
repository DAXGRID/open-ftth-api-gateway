using FluentResults;
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

namespace OpenFTTH.UtilityGraphService.Business.Trace
{
    /// <summary>
    /// As the name suggests, this class should be refactored into some more elegant trace functions
    /// </summary>
    public class SwissArmyKnifeTracer
    {
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly UtilityNetworkProjection _utilityNetwork;

        public SwissArmyKnifeTracer(IQueryDispatcher queryDispatcher, UtilityNetworkProjection utilityNetwork)
        {
            _queryDispatcher = queryDispatcher;
            _utilityNetwork = utilityNetwork;
        }

        public SwissArmyKnifeTraceResult? Trace(List<SpanEquipment> spanEquipmentsToTrace, Guid? traceThisSpanSegmentIdOnly)
        {
            if (spanEquipmentsToTrace.Count == 0)
                return null;

            var intermidiateTraceResult = GatherNetworkGraphTraceInformation(spanEquipmentsToTrace, traceThisSpanSegmentIdOnly);


            if (intermidiateTraceResult.InterestList.Count > 0)
            {
                var routeNetworkInformation = GatherRouteNetworkInformation(_queryDispatcher, intermidiateTraceResult.InterestList);

                var addressInformation = GatherAddressInformation(intermidiateTraceResult);

                if (routeNetworkInformation.Interests == null)
                    throw new ApplicationException("Failed to query route network interest information. Interest information is null");

                Dictionary<Guid, List<SpanSegmentRouteNetworkTraceRef>> traceIdRefBySpanEquipmentId = new();


                List<RouteNetworkTraceResult> routeNetworkTraces = new();

                Dictionary<Guid, UtilityNetworkTraceResult> utilityTraceBySpanSegmentId = new();

                // Find unique route network traces and utility traces
                foreach (var segmentWalksBySpanEquipmentId in intermidiateTraceResult.SegmentWalksBySpanEquipmentId)
                {
                    foreach (var segmentWalk in segmentWalksBySpanEquipmentId.Value)
                    {

                        // Find the segments covered by trace
                        List<Guid> routeSegmentIds = new();
                        List<Guid> spanSegmentIds = new();

                        foreach (var segmentHop in segmentWalk.Hops)
                        {
                            var walkIds = routeNetworkInformation.Interests[segmentHop.WalkOfInterestId].RouteNetworkElementRefs;

                            spanSegmentIds.Add(segmentHop.SpanSegmentId);

                            try
                            {
                                routeSegmentIds.AddRange(GetRouteSegmentsBetweenNodes(walkIds, segmentHop.FromNodeId, segmentHop.ToNodeId));
                            }
                            catch (ApplicationException ex)
                            {
                                Log.Error($"Error collecting route segments between route node: {segmentHop.FromNodeId} and route node: {segmentHop.ToNodeId} in walk of interest: {segmentHop.WalkOfInterestId} while tracing span segment: {segmentWalk.SpanEquipmentOrSegmentId} in span equipment: {segmentWalksBySpanEquipmentId.Key}. Error: {ex.Message}");
                            }
                        }

                        // Get the geometry of the segments
                        List<string> segmentGeometries = new();

                        foreach (var routeSegmentId in routeSegmentIds)
                        {
                            var segment = routeNetworkInformation.RouteNetworkElements[routeSegmentId];

                            if (segment.Coordinates != null)
                                segmentGeometries.Add(segment.Coordinates);
                        }


                        // Find from node id and name/description
                        var firstHop = segmentWalk.Hops.First();

                        Guid fromNodeId = firstHop.FromNodeId;

                        string? fromNodeName = routeNetworkInformation.RouteNetworkElements[fromNodeId].NamingInfo?.Name;

                        if (String.IsNullOrEmpty(fromNodeName))
                        {
                            fromNodeName = GetAddressInfoForHop(addressInformation, firstHop);
                        }


                        // Find to node id and name
                        var lastHop = segmentWalk.Hops.Last();

                        Guid toNodeId = lastHop.ToNodeId;

                        string? toNodeName = routeNetworkInformation.RouteNetworkElements[toNodeId].NamingInfo?.Name;

                        if (String.IsNullOrEmpty(toNodeName))
                        {
                            toNodeName = GetAddressInfoForHop(addressInformation, lastHop);
                        }


                        // Add route network trace (route segments)
                        Guid traceId = FindOrCreateRouteNetworkTrace(routeNetworkTraces, routeSegmentIds, segmentGeometries, fromNodeId, toNodeId, fromNodeName, toNodeName);

                        SpanSegmentRouteNetworkTraceRef traceRef = new SpanSegmentRouteNetworkTraceRef(segmentWalk.SpanEquipmentOrSegmentId, traceId);

                        if (!traceIdRefBySpanEquipmentId.ContainsKey(segmentWalksBySpanEquipmentId.Key))
                            traceIdRefBySpanEquipmentId[segmentWalksBySpanEquipmentId.Key] = new List<SpanSegmentRouteNetworkTraceRef>() { traceRef };
                        else
                            traceIdRefBySpanEquipmentId[segmentWalksBySpanEquipmentId.Key].Add(traceRef);


                        // Add utility network trace (span segments)
                        if (!utilityTraceBySpanSegmentId.ContainsKey(segmentWalk.SpanEquipmentOrSegmentId))
                        {
                            utilityTraceBySpanSegmentId[segmentWalk.SpanEquipmentOrSegmentId] = new UtilityNetworkTraceResult(
                                spanSegmentId: segmentWalk.SpanEquipmentOrSegmentId,
                                fromTerminalId: null,
                                toTerminalId: null,
                                spanSegmentIds: spanSegmentIds.ToArray()
                            );
                        }
                    }
                }

                return new SwissArmyKnifeTraceResult(routeNetworkTraces, traceIdRefBySpanEquipmentId, utilityTraceBySpanSegmentId);
            }
            else
                return null;
        }

        private static string? GetAddressInfoForHop(Dictionary<Guid, string> addressInformation, SegmentWalkHop lastHop)
        {
            if (lastHop.AddressInfo == null)
                return null;

            if (lastHop.AddressInfo.UnitAddressId != null && addressInformation.ContainsKey(lastHop.AddressInfo.UnitAddressId.Value))
            {
                return addressInformation[lastHop.AddressInfo.UnitAddressId.Value];
            }
            else if (lastHop.AddressInfo.AccessAddressId != null && addressInformation.ContainsKey(lastHop.AddressInfo.AccessAddressId.Value))
            {
                return addressInformation[lastHop.AddressInfo.AccessAddressId.Value];
            }
            else if (lastHop.AddressInfo.Remark != null)
                return lastHop.AddressInfo.Remark;

            return null;
        }

        private Guid FindOrCreateRouteNetworkTrace(List<RouteNetworkTraceResult> routeNetworkTraces, List<Guid> segmentIds, List<string> segmentGeometries, Guid fromNodeId, Guid toNodeId, string? fromNodeName, string? toNodeName)
        {
            foreach (var routeNetworkTrace in routeNetworkTraces)
            {
                if (routeNetworkTrace.RouteSegmentIds.SequenceEqual(segmentIds) && routeNetworkTrace.FromRouteNodeName == fromNodeName && routeNetworkTrace.ToRouteNodeName == toNodeName)
                    return routeNetworkTrace.Id;
            }

            var newRouteNetworkTrace = new API.Model.Trace.RouteNetworkTraceResult(Guid.NewGuid(), fromNodeId, toNodeId, segmentIds.ToArray(), fromNodeName, toNodeName, segmentGeometries.ToArray());

            routeNetworkTraces.Add(newRouteNetworkTrace);

            return newRouteNetworkTrace.Id;
        }

        private List<Guid> GetRouteSegmentsBetweenNodes(RouteNetworkElementIdList walkIds, Guid startNodeId, Guid endNodeId)
        {
            List<Guid> result = new();

            var startNodeIndex = walkIds.IndexOf(startNodeId);
            var endNodeIndex = walkIds.IndexOf(endNodeId);

            if (startNodeIndex < 0 || endNodeIndex < 0)
                throw new ApplicationException($"Failed to find start node: {startNodeId} or end node {endNodeId} in walk.");

            if (startNodeIndex < endNodeIndex)
            {
                for (int i = startNodeIndex + 1; i < endNodeIndex; i += 2)
                {
                    result.Add(walkIds[i]);
                }
            }
            else
            {
                for (int i = startNodeIndex - 1; i > endNodeIndex; i -= 2)
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
                    RouteNetworkElementFilter = new RouteNetworkElementFilterOptions() { IncludeNamingInfo = true, IncludeCoordinates = true }
                }
            ).Result;

            if (interestQueryResult.IsFailed)
                throw new ApplicationException("Failed to query route network information. Got error: " + interestQueryResult.Errors.First().Message);

            return interestQueryResult.Value;
        }

        private Dictionary<Guid, string> GatherAddressInformation(IntermidiateTraceResult intermidiateTraceResult)
        {
            HashSet<Guid> addressIdsToQuery = new();

            // Find eventually address id's in from and to end to query for
            foreach (var segmentWalksBySpanEquipmentId in intermidiateTraceResult.SegmentWalksBySpanEquipmentId)
            {
                foreach (var segmentWalk in segmentWalksBySpanEquipmentId.Value)
                {
                    var firstSegmentHop = segmentWalk.Hops.First();

                    if (firstSegmentHop.AddressInfo != null)
                    {
                        if (firstSegmentHop.AddressInfo.UnitAddressId != null && !addressIdsToQuery.Contains(firstSegmentHop.AddressInfo.UnitAddressId.Value))
                            addressIdsToQuery.Add(firstSegmentHop.AddressInfo.UnitAddressId.Value);
                        else if (firstSegmentHop.AddressInfo.AccessAddressId != null && !addressIdsToQuery.Contains(firstSegmentHop.AddressInfo.AccessAddressId.Value))
                            addressIdsToQuery.Add(firstSegmentHop.AddressInfo.AccessAddressId.Value);
                    }

                    var lastSegmentHop = segmentWalk.Hops.Last();

                    if (lastSegmentHop.AddressInfo != null)
                    {
                        if (lastSegmentHop.AddressInfo.UnitAddressId != null && !addressIdsToQuery.Contains(lastSegmentHop.AddressInfo.UnitAddressId.Value))
                            addressIdsToQuery.Add(lastSegmentHop.AddressInfo.UnitAddressId.Value);
                        else if (lastSegmentHop.AddressInfo.AccessAddressId != null && !addressIdsToQuery.Contains(lastSegmentHop.AddressInfo.AccessAddressId.Value))
                            addressIdsToQuery.Add(lastSegmentHop.AddressInfo.AccessAddressId.Value);
                    }
                }
            }

            if (addressIdsToQuery.Count == 0)
                return new Dictionary<Guid, string>();

            var getAddressInfoQuery = new GetAddressInfo(addressIdsToQuery.ToArray());

            var addressResult = _queryDispatcher.HandleAsync<GetAddressInfo, Result<GetAddressInfoResult>>(getAddressInfoQuery).Result;

            Dictionary<Guid, string> result = new();

            if (addressResult.IsSuccess)
            {
                foreach (var addressHit in addressResult.Value.AddressHits)
                {
                    if (addressHit.RefClass == AddressEntityClass.UnitAddress)
                    {
                        var unitAddress = addressResult.Value.UnitAddresses[addressHit.RefId];
                        var accessAddress = addressResult.Value.AccessAddresses[unitAddress.AccessAddressId];

                        var addressStr = accessAddress.RoadName + " " + accessAddress.HouseNumber;

                        if (unitAddress.FloorName != null)
                            addressStr += (", " + unitAddress.FloorName);

                        if (unitAddress.SuitName != null)
                            addressStr += (" " + unitAddress.SuitName);

                        result.Add(addressHit.Key, addressStr);
                    }
                    else
                    {
                        var accessAddress = addressResult.Value.AccessAddresses[addressHit.RefId];

                        var addressStr = accessAddress.RoadName + " " + accessAddress.HouseNumber;

                        result.Add(addressHit.Key, addressStr);
                    }
                }
            }
            else
            {
                Log.Error($"Error calling address service from trace. Error: " + addressResult.Errors.First().Message);
            }


            return result;
        }


        private IntermidiateTraceResult GatherNetworkGraphTraceInformation(List<SpanEquipment> spanEquipmentsToTrace, Guid? traceThisSpanSegmentIdOnly)
        {
            IntermidiateTraceResult result = new();

            // Trace all segments of all span equipments
            foreach (var spanEquipment in spanEquipmentsToTrace)
            {
                // Add walks for each span segment in the span equipment
                foreach (var spanStructure in spanEquipment.SpanStructures)
                {
                    foreach (var spanSegment in spanStructure.SpanSegments)
                    {
                        if (traceThisSpanSegmentIdOnly != null && traceThisSpanSegmentIdOnly != spanSegment.Id)
                            continue;

                        // On cables only trace outer jacket only always
                        if (traceThisSpanSegmentIdOnly == null && spanEquipment.IsCable && (spanStructure.Position != 1 || spanStructure.Level != 1))
                            continue;

                        var spanTraceResult = _utilityNetwork.Graph.SimpleTrace(spanSegment.Id);

                        // We're dealing with a connected segment if non-empty trace result is returned
                        if (spanTraceResult.Upstream.Length > 0)
                        {
                            var segmentWalk = new SegmentWalk(spanSegment.Id);

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
                                            fromNodeId:((UtilityGraphConnectedTerminal)spanTraceResult.Upstream[upstreamIndex - 1]).RouteNodeId,
                                            toNodeId: ((UtilityGraphConnectedTerminal)spanTraceResult.Upstream[upstreamIndex + 1]).RouteNodeId,
                                            walkOfInterestId: walkOfInterestId,
                                            addressInfo: ((UtilityGraphConnectedSegment)item).SpanEquipment(_utilityNetwork).AddressInfo
                                        );

                                        segmentWalk.Hops.Add(segmentHop);
                                    }
                                }
                            }

                            AddWalkToResult(result, spanEquipment, segmentWalk);
                        }
                        // We're dealing with an unconnected segment
                        else
                        {
                            if (_utilityNetwork.Graph.TryGetGraphElement<UtilityGraphDisconnectedSegment>(spanSegment.Id, out var disconnectedSegment))
                            {
                                var disconnectedSpanSegment = disconnectedSegment.SpanSegment(_utilityNetwork);

                                // if disconnected segment has been cut
                                if (traceThisSpanSegmentIdOnly != null || (disconnectedSpanSegment.FromNodeOfInterestIndex > 0 || disconnectedSpanSegment.ToNodeOfInterestIndex < (spanEquipment.NodesOfInterestIds.Length - 1)))
                                {
                                    var segmentHop = new SegmentWalkHop(
                                        spanEquipmentOrSegmentId: disconnectedSpanSegment.Id,
                                        fromNodeId: spanEquipment.NodesOfInterestIds[disconnectedSpanSegment.FromNodeOfInterestIndex],
                                        toNodeId: spanEquipment.NodesOfInterestIds[disconnectedSpanSegment.ToNodeOfInterestIndex],
                                        walkOfInterestId: spanEquipment.WalkOfInterestId,
                                        addressInfo: spanEquipment.AddressInfo
                                    );

                                    var segmentWalk = new SegmentWalk(spanSegment.Id);

                                    segmentWalk.Hops.Add(segmentHop);
                                    AddWalkToResult(result, spanEquipment, segmentWalk);

                                    AddWalkOfInterestToResult(result, disconnectedSegment);
                                }
                            }
                        }

                        // Break if cable
                        if (spanEquipment.IsCable)
                            break;
                    }
                }

                // Add walk that covers the whole span equipment, unless we're tracing specific segment that has no connections and cuts
                if (traceThisSpanSegmentIdOnly == null || result.SegmentWalksBySpanEquipmentId.Count == 0)
                {
                    var spanEquipmentSegmentHop = new SegmentWalkHop(
                        spanEquipmentOrSegmentId: spanEquipment.Id,
                        fromNodeId: spanEquipment.NodesOfInterestIds.First(),
                        toNodeId: spanEquipment.NodesOfInterestIds.Last(),
                        walkOfInterestId: spanEquipment.WalkOfInterestId,
                        addressInfo: spanEquipment.AddressInfo
                    );

                    // Snatch walk of interest id
                    if (!result.InterestList.Contains(spanEquipment.WalkOfInterestId))
                        result.InterestList.Add(spanEquipment.WalkOfInterestId);

                    var walk = new SegmentWalk(spanEquipment.Id) { Hops = new List<SegmentWalkHop>() { spanEquipmentSegmentHop } };

                    if (result.SegmentWalksBySpanEquipmentId.ContainsKey(spanEquipment.Id))
                    {
                        result.SegmentWalksBySpanEquipmentId[spanEquipment.Id].Add(walk);
                    }
                    else
                    {
                        result.SegmentWalksBySpanEquipmentId.Add(spanEquipment.Id, new List<SegmentWalk> { walk });
                    }
                }
            }

            return result;
        }

        private static void AddWalkToResult(IntermidiateTraceResult result, SpanEquipment spanEquipment, SegmentWalk segmentWalk)
        {
            if (!result.SegmentWalksBySpanEquipmentId.ContainsKey(spanEquipment.Id))
                result.SegmentWalksBySpanEquipmentId[spanEquipment.Id] = new List<SegmentWalk>() { segmentWalk };
            else
                result.SegmentWalksBySpanEquipmentId[spanEquipment.Id].Add(segmentWalk);
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
            public Dictionary<Guid, List<SegmentWalk>> SegmentWalksBySpanEquipmentId = new();
        }
    }
}
