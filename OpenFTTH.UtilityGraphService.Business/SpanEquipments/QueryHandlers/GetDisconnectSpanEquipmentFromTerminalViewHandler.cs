using DAX.ObjectVersioning.Graph;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.NodeContainers.Projections;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Projections;
using OpenFTTH.UtilityGraphService.Business.Trace.Util;
using OpenFTTH.UtilityGraphService.Business.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.QueryHandling
{
    public class DisconnectSpanEquipmentFromTerminalViewQueryHandler
        : IQueryHandler<GetDisconnectSpanEquipmentFromTerminalView, Result<DisconnectSpanEquipmentFromTerminalView>>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly UtilityNetworkProjection _utilityNetwork;
        private LookupCollection<SpanStructureSpecification> _spanStructureSpecifications;
        private LookupCollection<SpanEquipmentSpecification> _spanEquipmentSpecifications;

        public DisconnectSpanEquipmentFromTerminalViewQueryHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
        }

        public Task<Result<DisconnectSpanEquipmentFromTerminalView>> HandleAsync(GetDisconnectSpanEquipmentFromTerminalView query)
        {
            _spanStructureSpecifications = _eventStore.Projections.Get<SpanStructureSpecificationsProjection>().Specifications;
            _spanEquipmentSpecifications = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;

            if (!_utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphTerminalRef>(query.TerminalId, out var terminalRef))
                return Task.FromResult(Result.Fail<DisconnectSpanEquipmentFromTerminalView>(new GetEquipmentDetailsError(GetEquipmentDetailsErrorCodes.INVALID_QUERY_ARGUMENT_ERROR_LOOKING_UP_SPECIFIED_EQUIPMENT_BY_TERMINAL_ID, $"Invalid query. Cannot find any terminal equipment by the terminal id specified: {query.TerminalId}")));

            if (IsTerminalToTerminalLink(query.TerminalId, query.SpanSegmentId))
            {
                var link = GetTerminalToTerminalConnectivityLink(query.TerminalId, query.SpanSegmentId);


                return Task.FromResult(Result.Ok(BuildDisconnectLinkFromTerminalView(terminalRef, link)));

            }
            else
            {
                if (!_utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(query.SpanSegmentId, out var segmentRef))
                    return Task.FromResult(Result.Fail<DisconnectSpanEquipmentFromTerminalView>(new GetEquipmentDetailsError(GetEquipmentDetailsErrorCodes.INVALID_QUERY_ARGUMENT_ERROR_LOOKING_UP_SPECIFIED_EQUIPMENT_BY_SEGMENT_ID, $"Invalid query. Cannot find any span equipment by the span segment id specified: {query.SpanSegmentId}")));

                return Task.FromResult(Result.Ok(BuildDisconnectSpanEquipmentFromTerminalView(terminalRef, segmentRef)));
            }
        }

      

        private DisconnectSpanEquipmentFromTerminalView BuildDisconnectSpanEquipmentFromTerminalView(IUtilityGraphTerminalRef terminalRef, IUtilityGraphSegmentRef segmentRef)
        {
            var spanEquipment = segmentRef.SpanEquipment(_utilityNetwork);

            if (!_spanEquipmentSpecifications.TryGetValue(spanEquipment.SpecificationId, out var spanEquipmentSpecification))
                throw new ApplicationException($"Invalid/corrupted span equipment instance: {spanEquipment.Id} Has reference to non-existing span equipment specification with id: {spanEquipment.SpecificationId}");

            var equipmentData = GatherRelevantSpanEquipmentData(spanEquipment);


            int seqNo = 1;

            List<DisconnectSpanEquipmentFromTerminalViewConnection> lines = new();

            for (int spanStructureIndex = 1; spanStructureIndex < spanEquipment.SpanStructures.Length; spanStructureIndex++)
            {
                var spanStructure = spanEquipment.SpanStructures[spanStructureIndex];

                var spanSegmentToTrace = GetSpanSegmentToTrace(terminalRef.RouteNodeId, spanEquipment, spanStructure);

                var traceInfo = equipmentData.TracedSegments[spanSegmentToTrace.Id];

                var terminalEquipmentTraceEnd = GetTerminalEquipmentEnd(equipmentData, traceInfo, terminalRef.RouteNodeId);

                var oppositeTraceEnd = GetOppositeEquipmentEnd(equipmentData, traceInfo, terminalRef.RouteNodeId);

                string? equipmentName = null;
                string? equipmentStrutureName = null;
                string? equipmentTerminalName = null;

                if (terminalEquipmentTraceEnd != null)
                {
                    equipmentName = GetEquipmentName(equipmentData, terminalEquipmentTraceEnd.NeighborTerminal);
                    equipmentStrutureName = equipmentData.GetEquipmentStructureInfoString(terminalEquipmentTraceEnd.NeighborTerminal);
                    equipmentTerminalName = equipmentData.GetEquipmentTerminalInfoString(terminalEquipmentTraceEnd.NeighborTerminal);
                }

                lines.Add(
                    new DisconnectSpanEquipmentFromTerminalViewConnection(
                        isConnected: IsConnected(terminalEquipmentTraceEnd),
                        terminalId: terminalEquipmentTraceEnd != null ? terminalEquipmentTraceEnd.NeighborTerminal.Id : Guid.Empty,
                        segmentId: spanSegmentToTrace.Id,
                        spanStructurePosition: spanStructureIndex,
                        spanStructureName: equipmentData.GetSpanEquipmentTubeFiberString(spanEquipment, spanStructureIndex),
                        terminalEquipmentName: equipmentName,
                        terminalStructureName: equipmentStrutureName,
                        terminalName: equipmentTerminalName,
                        end: IsConnected(terminalEquipmentTraceEnd) ? equipmentData.GetNodeAndEquipmentEndString(oppositeTraceEnd.EndTerminal) : null
                    )
                );

                seqNo++;
            }

            return new DisconnectSpanEquipmentFromTerminalView(spanEquipment.Name, lines.ToArray());
        }

        private DisconnectSpanEquipmentFromTerminalView BuildDisconnectLinkFromTerminalView(IUtilityGraphTerminalRef terminalRef, UtilityGraphTerminalToTerminalConnectivityLink link)
        {
            List<DisconnectSpanEquipmentFromTerminalViewConnection> lines = new();

            var oppositeEndTerminal = GetOppositeTerminal(link, terminalRef.TerminalId);

            if (oppositeEndTerminal != null)
            {
                var equipmentData = GatherRelevantSpanEquipmentData(oppositeEndTerminal.Id);

                int seqNo = 1;

                var traceInfo = equipmentData.TracedSegments[oppositeEndTerminal.Id];

                string? equipmentName = null;
                string? equipmentStrutureName = null;
                string? equipmentTerminalName = null;

                equipmentName = GetEquipmentName(equipmentData, oppositeEndTerminal);
                equipmentStrutureName = equipmentData.GetEquipmentStructureInfoString(oppositeEndTerminal);
                equipmentTerminalName = equipmentData.GetEquipmentTerminalInfoString(oppositeEndTerminal);

                lines.Add(
                    new DisconnectSpanEquipmentFromTerminalViewConnection(
                        isConnected: true,
                        terminalId: oppositeEndTerminal.TerminalId,
                        segmentId: link.Id,
                        spanStructurePosition: 1,
                        spanStructureName: "patch/pigtail",
                        terminalEquipmentName: equipmentName,
                        terminalStructureName: equipmentStrutureName,
                        terminalName: equipmentTerminalName,
                        end: null
                    )
                );

                seqNo++;
            }

            return new DisconnectSpanEquipmentFromTerminalView("patch/pigtail", lines.ToArray());
        }

        private string GetEquipmentName(RelevantEquipmentData equipmentData, UtilityGraphConnectedTerminal neighborTerminal)
        {
            if (neighborTerminal.IsDummyEnd)
            {
                return "løs ende";
            }
            else
            {
                var terminalEquipment = neighborTerminal.TerminalEquipment(_utilityNetwork);

                if (terminalEquipment != null)
                {
                    return equipmentData.GetFullEquipmentString(neighborTerminal.RouteNodeId, neighborTerminal.TerminalEquipment(_utilityNetwork), true);
                }
                else
                {
                    return "løs ende";
                }
            }
        }

        private bool IsConnected(TraceEndInfo? oppositeTraceEnd)
        {
            return oppositeTraceEnd != null;
        }

        private TraceEndInfo? GetTerminalEquipmentEnd(RelevantEquipmentData equipmentData, TraceInfo? traceInfo, Guid routeNodeId)
        {
            if (traceInfo == null)
                return null;

            if (traceInfo.A != null && traceInfo.A.NeighborTerminal.RouteNodeId == routeNodeId)
                return traceInfo.A;
            else if (traceInfo.Z != null && traceInfo.Z.NeighborTerminal.RouteNodeId == routeNodeId)
                return traceInfo.Z;

            return null;
        }

        private TraceEndInfo? GetOppositeEquipmentEnd(RelevantEquipmentData equipmentData, TraceInfo? traceInfo, Guid routeNodeId)
        {
            if (traceInfo == null)
                return null;

            if (traceInfo.A != null && traceInfo.A.NeighborTerminal.RouteNodeId != routeNodeId)
                return traceInfo.A;
            else if (traceInfo.Z != null && traceInfo.Z.NeighborTerminal.RouteNodeId != routeNodeId)
                return traceInfo.Z;

            return null;
        }

        private SpanSegment GetSpanSegmentToTrace(Guid routeNetworkElementId, SpanEquipment spanEquipment, SpanStructure spanStructure)
        {
            foreach (var spanSegment in spanStructure.SpanSegments)
            {
                if (spanEquipment.NodesOfInterestIds[spanSegment.FromNodeOfInterestIndex] == routeNetworkElementId || spanEquipment.NodesOfInterestIds[spanSegment.ToNodeOfInterestIndex] == routeNetworkElementId)
                    return spanSegment;
            }

            throw new ApplicationException($"Error locating a span segment in span equipment: {spanEquipment.Id} structure position: {spanStructure.Position} that start or ends in route node id: {routeNetworkElementId}");
        }

        private RelevantEquipmentData GatherRelevantSpanEquipmentData(SpanEquipment spanEquipment)
        {
            var tracedSegments = TraceAllSegments(spanEquipment);

            var endNodesIds = GetEndNodeIdsFromTraceResult(tracedSegments.Values);

            RelevantEquipmentData relevantEquipmentData = new RelevantEquipmentData(_eventStore, _utilityNetwork, _queryDispatcher, endNodesIds);

            relevantEquipmentData.TracedSegments = tracedSegments;

            return relevantEquipmentData;
        }

        private RelevantEquipmentData GatherRelevantSpanEquipmentData(Guid terminalId)
        {
            // Get opposite link terminal
            var tracedSegments = TraceOneTerminal(terminalId);

            var endNodesIds = GetEndNodeIdsFromTraceResult(tracedSegments.Values);

            RelevantEquipmentData relevantEquipmentData = new RelevantEquipmentData(_eventStore, _utilityNetwork, _queryDispatcher, endNodesIds);

            relevantEquipmentData.TracedSegments = tracedSegments;

            return relevantEquipmentData;
        }

        private UtilityGraphConnectedTerminal? GetOppositeTerminal(UtilityGraphTerminalToTerminalConnectivityLink link, Guid terminalId)
        {
            var version = _utilityNetwork.Graph.LatestCommitedVersion;

            var neighbors = link.NeighborElements(version).ToArray();

            return link.NeighborElements(version).FirstOrDefault(n => n.Id != terminalId) as UtilityGraphConnectedTerminal;
        }

        private Dictionary<Guid, TraceInfo> TraceAllSegments(SpanEquipment spanEquipment)
        {
            Dictionary<Guid, TraceInfo> traceInfosByTerminalId = new();

            // Trace all equipment terminals
            foreach (var spanStructure in spanEquipment.SpanStructures)
            {
                foreach (var segment in spanStructure.SpanSegments)
                {
                    TraceInfo traceInfo = new TraceInfo();

                    var terminalTraceResult = _utilityNetwork.Graph.SimpleTrace(segment.Id);

                    if (terminalTraceResult != null)
                    {
                        if (terminalTraceResult.Upstream.Length > 0)
                        {
                            traceInfo.Upstream = GetEndInfoFromTrace(segment.Id, terminalTraceResult.Upstream);
                        }

                        if (terminalTraceResult.Downstream.Length > 0)
                        {
                            traceInfo.Downstream = GetEndInfoFromTrace(segment.Id, terminalTraceResult.Downstream);
                        }
                    }

                    traceInfosByTerminalId.Add(segment.Id, traceInfo);
                }
            }

            return traceInfosByTerminalId;
        }

        private Dictionary<Guid, TraceInfo> TraceOneTerminal(Guid terminalId)
        {
            Dictionary<Guid, TraceInfo> traceInfosByTerminalId = new();

            TraceInfo traceInfo = new TraceInfo();

            var terminalTraceResult = _utilityNetwork.Graph.SimpleTrace(terminalId);

            if (terminalTraceResult != null)
            {
                if (terminalTraceResult.Upstream.Length > 0)
                {
                    traceInfo.Upstream = GetEndInfoFromTrace(terminalId, terminalTraceResult.Upstream);
                }

                if (terminalTraceResult.Downstream.Length > 0)
                {
                    traceInfo.Downstream = GetEndInfoFromTrace(terminalId, terminalTraceResult.Downstream);
                }
            }

            traceInfosByTerminalId.Add(terminalId, traceInfo);

            return traceInfosByTerminalId;
        }

        private IEnumerable<Guid> GetEndNodeIdsFromTraceResult(IEnumerable<TraceInfo> traceInfos)
        {
            HashSet<Guid> endNodeIds = new();

            foreach (var traceInfo in traceInfos)
            {
                AddEndNodeIdsToHash(traceInfo, endNodeIds);
            }

            return endNodeIds;
        }

        private static void AddEndNodeIdsToHash(TraceInfo traceInfo, HashSet<Guid> endNodeIds)
        {
            if (traceInfo.Upstream != null)
            {
                if (!endNodeIds.Contains(traceInfo.Upstream.EndTerminal.RouteNodeId))
                    endNodeIds.Add(traceInfo.Upstream.EndTerminal.RouteNodeId);
            }

            if (traceInfo.Downstream != null)
            {
                if (!endNodeIds.Contains(traceInfo.Downstream.EndTerminal.RouteNodeId))
                    endNodeIds.Add(traceInfo.Downstream.EndTerminal.RouteNodeId);
            }
        }

        private TraceEndInfo GetEndInfoFromTrace(Guid tracedSpanSegmentId, IGraphObject[] trace)
        {
            if (trace.Length < 2)
                throw new ApplicationException($"Expected trace length to be minimum 2. Please check trace on span segment with id: {tracedSpanSegmentId}");


            // Get neighbor terminal
            var neighborTerminal = trace[1];

            if (!(neighborTerminal is UtilityGraphConnectedTerminal))
                throw new ApplicationException($"Expected neighbor to be a UtilityGraphConnectedTerminal. Please check trace on span segment with id: {tracedSpanSegmentId}");


            // Get end terminal
            var terminalEnd = trace.Last();

            if (!(terminalEnd is UtilityGraphConnectedTerminal))
                throw new ApplicationException($"Expected end to be a UtilityGraphConnectedTerminal. Please check trace on span segment with id: {tracedSpanSegmentId}");


            return new TraceEndInfo((UtilityGraphConnectedTerminal)neighborTerminal, (UtilityGraphConnectedTerminal)terminalEnd);
        }

        private bool IsTerminalToTerminalLink(Guid terminalId, Guid segmentId)
        {
            var link = GetTerminalToTerminalConnectivityLink(terminalId, segmentId);

            if (link != null)
                return true;
            else
                return false;
        }

        private UtilityGraphTerminalToTerminalConnectivityLink? GetTerminalToTerminalConnectivityLink(Guid terminalId, Guid terminalToTerminalLinkId)
        {
            if (!_utilityNetwork.Graph.TryGetGraphElement<UtilityGraphConnectedTerminal>(terminalId, out var terminal))
                throw new ApplicationException($"Error looking up connected terminal by id: {terminalId}");

            var version = _utilityNetwork.Graph.LatestCommitedVersion;

            var terminalToTerminalLink = terminal.NeighborElements(version).FirstOrDefault(n => n.Id == terminalToTerminalLinkId) as UtilityGraphTerminalToTerminalConnectivityLink;

            return terminalToTerminalLink;
        }

        private class RelevantEquipmentData : RelatedDataHolder
        {
            public Dictionary<Guid, TraceInfo> TracedSegments { get; set; }

            public RelevantEquipmentData(IEventStore eventStore, UtilityNetworkProjection utilityNetwork, IQueryDispatcher queryDispatcher, IEnumerable<Guid> nodeOfInterestIds)
                : base(eventStore, utilityNetwork, queryDispatcher, nodeOfInterestIds)
            {
            }
        }

        private record TraceInfo
        {
            public TraceEndInfo? Upstream { get; set; }
            public TraceEndInfo? Downstream { get; set; }

            public bool UpstreamIsZ {get; set;}
            public TraceEndInfo? Z
            {
                get
                {
                    if (UpstreamIsZ) return Upstream;
                    else return Downstream;
                }
            }

            public TraceEndInfo? A
            {
                get
                {
                    if (!UpstreamIsZ) return Upstream;
                    else return Downstream;
                }
            }

        }

        private record TraceEndInfo
        {
            public UtilityGraphConnectedTerminal NeighborTerminal { get; set; }
            public UtilityGraphConnectedTerminal EndTerminal { get; set; }

            public TraceEndInfo(UtilityGraphConnectedTerminal neighborTerminal, UtilityGraphConnectedTerminal endTerminal)
            {
                NeighborTerminal = neighborTerminal;
                EndTerminal = endTerminal;
            }
        }
    }
}
