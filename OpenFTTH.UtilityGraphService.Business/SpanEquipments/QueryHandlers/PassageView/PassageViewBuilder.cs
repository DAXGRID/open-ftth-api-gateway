using DAX.ObjectVersioning.Graph;
using NetTopologySuite.Geometries;
using Newtonsoft.Json.Linq;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using OpenFTTH.UtilityGraphService.Business.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.QueryHandlers.PassageView
{
    public abstract class PassageViewBuilder
    {
        private readonly IEventStore _eventStore;

        protected readonly IQueryDispatcher _queryDispatcher;
        protected readonly Guid _routeNetworkElementId;
        protected readonly SpanEquipment _spanEquipment;
        protected readonly Guid? _segmentIdToTrace;

        protected readonly UtilityNetworkProjection _utilityNetwork;

        protected LookupCollection<SpanStructureSpecification> _spanStructureSpecifications;
        protected LookupCollection<SpanEquipmentSpecification> _spanEquipmentSpecifications;

        protected LookupCollection<RouteNetworkElement> _routeNetworkElements;
        protected LookupCollection<RouteNetworkInterest> _routeNetworkInterests;

        protected SpanEquipmentSpecification _spanEquipmentSpecification;


        protected PassageViewBuilder(IEventStore eventStore, UtilityNetworkProjection utilityNetwork, IQueryDispatcher queryDispatcher, Guid routeNetworkElementId, SpanEquipment spanEquipment, Guid? segmentIdToTrace = null)
        {
            _eventStore = eventStore;
            _utilityNetwork = utilityNetwork;
            _queryDispatcher = queryDispatcher;
            _routeNetworkElementId = routeNetworkElementId;
            _spanEquipment = spanEquipment;
            _segmentIdToTrace = segmentIdToTrace;

            _spanStructureSpecifications = _eventStore.Projections.Get<SpanStructureSpecificationsProjection>().Specifications;
            _spanEquipmentSpecifications = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;


            var interestIdsToQuery = GetInterestIdsToQuery();

            // Query all
            InterestIdList interestIds = new();
            interestIds.AddRange(interestIdsToQuery);

            QueryHelper.GetInterestsAndRouteNetworkElementsByInterestIds(_queryDispatcher, interestIds, out var routeNetworkInterests, out var routeNetworkElements);
            _routeNetworkInterests = routeNetworkInterests;
            _routeNetworkElements = routeNetworkElements;

            // Get the specification
            if (!_spanEquipmentSpecifications.TryGetValue(_spanEquipment.SpecificationId, out var spanEquipmentSpecification))
                throw new ApplicationException($"Invalid/corrupted span equipment instance: {_spanEquipment.Id} Has reference to non-existing span equipment specification with id: {_spanEquipment.SpecificationId}");

            _spanEquipmentSpecification = spanEquipmentSpecification;
        }

        protected HashSet<Guid> GetInterestIdsToQuery()
        {
            HashSet<Guid> interestIdsToQuery = new();

            // The span equipment itself
            interestIdsToQuery.Add(_spanEquipment.WalkOfInterestId);

            // Add walk of interest ids for all span equipments the conduit might be related to
            if (_spanEquipment.UtilityNetworkHops != null)
            {
                foreach (var utilityHop in _spanEquipment.UtilityNetworkHops)
                {
                    if (utilityHop.ParentAffixes != null && utilityHop.ParentAffixes.Count() > 0)
                    {
                        var firstSegmentId = utilityHop.ParentAffixes[0].SpanSegmentId;

                        var spanTraceResult = _utilityNetwork.Graph.SimpleTrace(firstSegmentId);

                        // Add the interest of the span equipment holding the first span segment
                        if (_utilityNetwork.TryGetEquipment<SpanEquipment>(firstSegmentId, out var spanEquipment))
                            interestIdsToQuery.Add(spanEquipment.WalkOfInterestId);

                        // Add eventually upstream span equipment interest
                        foreach (var traceObject in spanTraceResult.Upstream)
                        {
                            if (traceObject is IUtilityGraphSegmentRef connectedSegment)
                            {
                                interestIdsToQuery.Add(connectedSegment.SpanEquipment(_utilityNetwork).WalkOfInterestId);
                            }
                        }

                        // Add eventually downstream span equipment interests
                        foreach (var traceObject in spanTraceResult.Downstream)
                        {
                            if (traceObject is IUtilityGraphSegmentRef connectedSegment)
                            {
                                interestIdsToQuery.Add(connectedSegment.SpanEquipment(_utilityNetwork).WalkOfInterestId);
                            }
                        }
                    }
                }
            }

            if (_segmentIdToTrace != null)
            {
                var spanTraceResult = _utilityNetwork.Graph.SimpleTrace(_segmentIdToTrace.Value);

                // Add the interest of the span equipment holding the first span segment
                if (_utilityNetwork.TryGetEquipment<SpanEquipment>(_segmentIdToTrace.Value, out var spanEquipment))
                    interestIdsToQuery.Add(spanEquipment.WalkOfInterestId);

                // Add eventually upstream span equipment interest
                foreach (var traceObject in spanTraceResult.Upstream)
                {
                    if (traceObject is IUtilityGraphSegmentRef connectedSegment)
                    {
                        interestIdsToQuery.Add(connectedSegment.SpanEquipment(_utilityNetwork).WalkOfInterestId);
                    }
                }

                // Add eventually downstream span equipment interests
                foreach (var traceObject in spanTraceResult.Downstream)
                {
                    if (traceObject is IUtilityGraphSegmentRef connectedSegment)
                    {
                        interestIdsToQuery.Add(connectedSegment.SpanEquipment(_utilityNetwork).WalkOfInterestId);
                    }
                }
            }

            return interestIdsToQuery;
        }

        protected List<SpanEquipmentPassageViewLineInfo> CreateLineInfosForConduitHops(Guid spanSegmentIdToTrace, Guid fromNodeId, Guid toNodeId, ref double totalLength)
        {
            List<SpanEquipmentPassageViewLineInfo> linesToReturn = new();

            var segmentsTraced = TraceSegment(fromNodeId, toNodeId, spanSegmentIdToTrace);

            foreach (var segmentTrace in segmentsTraced)
            {
                var traceWoi = GetSpanSegmentWalkOfInterest(segmentTrace.SpanEquipment, segmentTrace.SpanSegment);
                var spanSegmentGeometries = GetSpanSegmentGeometries(traceWoi);

                var length = GetLineStringsLength(spanSegmentGeometries);
                totalLength += length;

                var lineInfo = new SpanEquipmentPassageViewLineInfo(segmentTrace.SpanSegment.Id)
                {
                    FromNodeId = segmentTrace.FromNodeId,
                    ToNodeId = segmentTrace.ToNodeId,
                    From = GetNodeName(segmentTrace.FromNodeId),
                    To = GetNodeName(segmentTrace.ToNodeId),
                    ConduitId = segmentTrace.SpanEquipment.Name,
                    OuterConduitInfo = GetOuterConduitInfo(segmentTrace),
                    InnerConduitInfo = GetInnerConduitInfo(segmentTrace),
                    RouteSegmentIds = traceWoi.SegmentIds.ToArray(),
                    RouteSegmentGeometries = spanSegmentGeometries.ToArray(),
                    SegmentLength = length,
                    CumulativeDistance = totalLength
                };

                linesToReturn.Add(lineInfo);
            }

            return linesToReturn;
        }

        protected List<SegmentTraceHopInfo> TraceSegment(Guid fromNodeId, Guid toNodeId, Guid segmentIdToTrace)
        {
            List<SegmentTraceHopInfo> tracedHopsToReturn = new();

            var spanTraceResult = _utilityNetwork.Graph.SimpleTrace(segmentIdToTrace);

            // If hop is a disconnected segment
            if (spanTraceResult.Source == null)
            {
                if (_utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(segmentIdToTrace, out var spanSegmentGraphElement))
                {
                    var spanEquipment = spanSegmentGraphElement.SpanEquipment(_utilityNetwork);
                    var spanStructure = spanEquipment.SpanStructures[spanSegmentGraphElement.StructureIndex];
                    var spanSegment = spanSegmentGraphElement.SpanSegment(_utilityNetwork);

                    if (!_spanEquipmentSpecifications.TryGetValue(spanEquipment.SpecificationId, out var spanEquipmentSpecification))
                        throw new ApplicationException($"Invalid/corrupted span equipment instance: {spanEquipment.Id} Has reference to non-existing span equipment specification with id: {spanEquipment.SpecificationId}");

                    if (!_spanStructureSpecifications.TryGetValue(spanStructure.SpecificationId, out var spanStructureSpecification))
                        throw new ApplicationException($"Invalid/corrupted span structure specification: {spanEquipment.SpecificationId} has reference to non-existing span structure specification with id: {spanStructure.SpecificationId}");

                    tracedHopsToReturn.Add(
                        new SegmentTraceHopInfo(
                            fromNodeId: fromNodeId != Guid.Empty ? fromNodeId : spanEquipment.NodesOfInterestIds[spanSegment.FromNodeOfInterestIndex],
                            toNodeId: toNodeId != Guid.Empty ? toNodeId : spanEquipment.NodesOfInterestIds[spanSegment.ToNodeOfInterestIndex],
                            spanEquipment: spanEquipment,
                            spanStructureIndex: spanSegmentGraphElement.StructureIndex,
                            spanSegment: spanSegment,
                            spanEquipmentSpecification: spanEquipmentSpecification,
                            spanStructureSpecification: spanStructureSpecification
                        )
                    );
                }
                else
                    throw new ApplicationException($"Utility hop inconsistency in span equipment with id: {_spanEquipment.Id} The segment id: {segmentIdToTrace}, found in a utility network hop, do not point to a segment!");
            }
            else
            {
                var graphTracedHops = ReverseIfNeeded(spanTraceResult.All, fromNodeId, toNodeId);

                // Snatch the segments, from and to terminals of a structure looking like this: (terminal)-(segment)-(terminal)-(segment)-(terminal)
                for (int graphTracedHopsIndex = 1; graphTracedHopsIndex < graphTracedHops.Count - 1; graphTracedHopsIndex += 2)
                {
                    var segment = graphTracedHops[graphTracedHopsIndex] as UtilityGraphConnectedSegment;
                    var fromTerminal = graphTracedHops[graphTracedHopsIndex - 1] as UtilityGraphConnectedTerminal;
                    var toTerminal = graphTracedHops[graphTracedHopsIndex + 1] as UtilityGraphConnectedTerminal;


                    if (segment == null)
                        throw new ApplicationException($"Utility hop inconsistency in span equipment with id: {_spanEquipment.Id} Tracing the segment with id: {segmentIdToTrace} resulted in unexpected result. Expected a connected segment at pos: {graphTracedHopsIndex} but got: {graphTracedHops[graphTracedHopsIndex]}");

                    if (fromTerminal == null)
                        throw new ApplicationException($"Utility hop inconsistency in span equipment with id: {_spanEquipment.Id} Tracing the segment with id: {segmentIdToTrace} resulted in unexpected result. Expected a connected terminal at pos: {graphTracedHopsIndex - 1} but got: {graphTracedHops[graphTracedHopsIndex - 1]}");

                    if (toTerminal == null)
                        throw new ApplicationException($"Utility hop inconsistency in span equipment with id: {_spanEquipment.Id} Tracing the segment with id: {segmentIdToTrace} resulted in unexpected result. Expected a connected terminal at pos: {graphTracedHopsIndex + 1} but got: {graphTracedHops[graphTracedHopsIndex + 1]}");


                    var spanEquipment = segment.SpanEquipment(_utilityNetwork);
                    var spanStructure = spanEquipment.SpanStructures[segment.StructureIndex];
                    var spanSegment = segment.SpanSegment(_utilityNetwork);

                    if (!_spanEquipmentSpecifications.TryGetValue(spanEquipment.SpecificationId, out var spanEquipmentSpecification))
                        throw new ApplicationException($"Invalid/corrupted span equipment instance: {spanEquipment.Id} Has reference to non-existing span equipment specification with id: {spanEquipment.SpecificationId}");

                    if (!_spanStructureSpecifications.TryGetValue(spanStructure.SpecificationId, out var spanStructureSpecification))
                        throw new ApplicationException($"Invalid/corrupted span structure specification: {spanEquipment.SpecificationId} has reference to non-existing span structure specification with id: {spanStructure.SpecificationId}");


                    tracedHopsToReturn.Add(
                        new SegmentTraceHopInfo(
                            fromNodeId: fromTerminal.RouteNodeId,
                            toNodeId: toTerminal.RouteNodeId,
                            spanEquipment: spanEquipment,
                            spanStructureIndex: segment.StructureIndex,
                            spanSegment: spanSegment,
                            spanEquipmentSpecification: spanEquipmentSpecification,
                            spanStructureSpecification: spanStructureSpecification
                       )
                    );
                }
            }

            return tracedHopsToReturn;
        }

        protected List<IGraphObject> ReverseIfNeeded(List<IGraphObject> trace, Guid fromNodeId, Guid toNodeId)
        {
            if (trace.Count > 0)
            {
                var firstElement = trace.First();

                if (firstElement is UtilityGraphConnectedTerminal connectedTerminal)
                {
                    if (connectedTerminal.RouteNodeId == toNodeId)
                    {
                        trace.Reverse();
                    }
                }
            }

            return trace;
        }


        protected string? GetName()
        {
            if (_spanEquipment.MarkingInfo != null && _spanEquipment.MarkingInfo.MarkingText != null)
            {
                return $"{_spanEquipment.Name} ({ _spanEquipment.MarkingInfo.MarkingText})";
            }
            else
            {
                return _spanEquipment.Name;
            }
        }

        protected static string GetOuterConduitInfo(SegmentTraceHopInfo segmentTrace)
        {
            if (segmentTrace.SpanEquipment.MarkingInfo != null && segmentTrace.SpanEquipment.MarkingInfo.MarkingText != null)
            {
                return $"{segmentTrace.SpanEquipmentSpecification.Name} ({ segmentTrace.SpanEquipment.MarkingInfo.MarkingText})";
            }
            else
            {
                return segmentTrace.SpanEquipmentSpecification.Name;
            }
        }

        protected static string? GetInnerConduitInfo(SegmentTraceHopInfo segmentTrace)
        {
            if (segmentTrace.SpanStructureIndex > 0)
            {
                return $"{segmentTrace.SpanStructureIndex} ({segmentTrace.SpanStructureSpecification.Color})";
            }
            else
            {
                return null;
            }
        }

        protected ValidatedRouteNetworkWalk GetSpanSegmentWalkOfInterest(SpanEquipment spanEquipment, SpanSegment spanSegment)
        {
            var walkOfInterest = _routeNetworkInterests[spanEquipment.WalkOfInterestId];

            var fromNodeId = spanEquipment.NodesOfInterestIds[spanSegment.FromNodeOfInterestIndex];
            var toNodeId = spanEquipment.NodesOfInterestIds[spanSegment.ToNodeOfInterestIndex];

            RouteNetworkElementIdList result = new();

            bool fromNodeFound = false;
            bool toNodeFound = false;

            for (int woiIndex = 0; woiIndex < walkOfInterest.RouteNetworkElementRefs.Count; woiIndex++)
            {
                if (walkOfInterest.RouteNetworkElementRefs[woiIndex] == fromNodeId)
                {
                    fromNodeFound = true;
                }

                if (fromNodeFound)
                    result.Add(walkOfInterest.RouteNetworkElementRefs[woiIndex]);

                if (walkOfInterest.RouteNetworkElementRefs[woiIndex] == toNodeId)
                {
                    toNodeFound = true;
                    break;
                }
            }

            if (!fromNodeFound)
            {
                throw new ApplicationException($"Error creating subwalk from node: {fromNodeId} to node: {toNodeId} in span equipment with id: {spanEquipment.Id} The from node id was never found! There must be inconsistency in {spanSegment.Id}");
            }

            if (!toNodeFound)
            {
                throw new ApplicationException($"Error creating subwalk from node: {fromNodeId} to node: {toNodeId} in span equipment with id: {spanEquipment.Id} The to node id was never found! There must be inconsistency in span segment: {spanSegment.Id}");
            }

            return new ValidatedRouteNetworkWalk(result);
        }

        protected List<string> GetSpanSegmentGeometries(ValidatedRouteNetworkWalk traceWoi)
        {
            List<string> result = new();

            foreach (var segmentId in traceWoi.SegmentIds)
            {
                if (_routeNetworkElements.ContainsKey(segmentId))
                {
                    var routeSegment = _routeNetworkElements[segmentId];

                    if (routeSegment.Coordinates != null)
                        result.Add(routeSegment.Coordinates);
                }
            }

            return result;
        }

        protected static double GetLineStringsLength(List<string> lineStringJsons)
        {
            double length = 0;

            foreach (var line in lineStringJsons)
            {
                length += GetLineStringLength(line);
            }

            return length;
        }

        protected static double GetLineStringLength(string lineStringJson)
        {
            List<Coordinate> coordinates = new();

            var coordPairs = JArray.Parse(lineStringJson);
            foreach (var coordPair in coordPairs)
            {
                coordinates.Add(new Coordinate(((JArray)coordPair)[0].Value<double>(), ((JArray)coordPair)[1].Value<double>()));
            }

            return new LineString(coordinates.ToArray()).Length;
        }

        protected string? GetNodeName(Guid nodeId)
        {
            if (_routeNetworkElements.ContainsKey(nodeId))
            {
                var routeNetworkElement = _routeNetworkElements[nodeId];

                if (routeNetworkElement.NamingInfo != null)
                    return routeNetworkElement.NamingInfo.Name;
            }

            return null;
        }

        protected Guid TryFindAEndNodeId(Guid nodeId1, Guid nodeId2)
        {
            var node1 = _routeNetworkElements[nodeId1];

            if (node1.RouteNodeInfo == null || node1.RouteNodeInfo.Function == null)
                return nodeId1;

            var node1Rank = (int)node1.RouteNodeInfo.Function;

            var node2 = _routeNetworkElements[nodeId2];

            if (node2.RouteNodeInfo == null || node2.RouteNodeInfo.Function == null)
                return nodeId1;

            var node2Rank = (int)node2.RouteNodeInfo.Function;

            if (node2Rank < node1Rank)
                return nodeId2;
            else
                return nodeId1;
        }

        protected void ReverseLineInfos(List<SpanEquipmentPassageViewLineInfo> lineInfos)
        {
            lineInfos.Reverse();

            double totalLength = 0;

            foreach (var lineInfo in lineInfos)
            {
                totalLength += lineInfo.SegmentLength;
                lineInfo.Reverse(totalLength);
            }
        }

        protected class SegmentTraceHopInfo
        {
            public Guid FromNodeId { get; }
            public Guid ToNodeId { get; }
            public SpanEquipment SpanEquipment { get; }
            public ushort SpanStructureIndex { get; }
            public SpanSegment SpanSegment { get; }
            public SpanEquipmentSpecification SpanEquipmentSpecification { get; }
            public SpanStructureSpecification SpanStructureSpecification { get; }

            public SegmentTraceHopInfo(Guid fromNodeId, Guid toNodeId, SpanEquipment spanEquipment, ushort spanStructureIndex, SpanSegment spanSegment, SpanEquipmentSpecification spanEquipmentSpecification, SpanStructureSpecification spanStructureSpecification)
            {
                FromNodeId = fromNodeId;
                ToNodeId = toNodeId;
                SpanEquipment = spanEquipment;
                SpanStructureIndex = spanStructureIndex;
                SpanSegment = spanSegment;
                SpanEquipmentSpecification = spanEquipmentSpecification;
                SpanStructureSpecification = spanStructureSpecification;
            }
        }
    }

}
