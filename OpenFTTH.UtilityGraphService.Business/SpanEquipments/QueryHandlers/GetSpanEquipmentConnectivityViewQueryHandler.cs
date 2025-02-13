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
    public class GetSpanEquipmentConnectivityViewQueryHandler
        : IQueryHandler<GetSpanEquipmentConnectivityView, Result<SpanEquipmentAZConnectivityViewModel>>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly UtilityNetworkProjection _utilityNetwork;
        private LookupCollection<SpanStructureSpecification> _spanStructureSpecifications;
        private LookupCollection<SpanEquipmentSpecification> _spanEquipmentSpecifications;

        public GetSpanEquipmentConnectivityViewQueryHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
        }

        public Task<Result<SpanEquipmentAZConnectivityViewModel>> HandleAsync(GetSpanEquipmentConnectivityView query)
        {
            _spanStructureSpecifications = _eventStore.Projections.Get<SpanStructureSpecificationsProjection>().Specifications;
            _spanEquipmentSpecifications = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;

            List<SpanEquipmentAZConnectivityViewEquipmentInfo> spanEquipmentViewInfos = new();

            foreach (var spanEquipmentOrSegmentId in query.SpanEquipmentOrSegmentIds)
            {
                if (_utilityNetwork.TryGetEquipment<SpanEquipment>(spanEquipmentOrSegmentId, out var spanEquipment))
                {
                    spanEquipmentViewInfos.Add(BuildSpanEquipmentView(query, spanEquipment, _spanEquipmentSpecifications, _spanStructureSpecifications));
                }
                else
                {
                    return Task.FromResult(Result.Fail<SpanEquipmentAZConnectivityViewModel>(new GetEquipmentDetailsError(GetEquipmentDetailsErrorCodes.INVALID_QUERY_ARGUMENT_ERROR_LOOKING_UP_SPECIFIED_EQUIPMENT_BY_EQUIPMENT_ID, $"Invalid query. Cannot find any span equipment by the equipment or span segment id specified: {spanEquipmentOrSegmentId}")));
                }
            }

            return Task.FromResult(
                      Result.Ok(
                          new SpanEquipmentAZConnectivityViewModel(
                                  spanEquipments: spanEquipmentViewInfos.ToArray()
                          )
                      )
                  );
        }

        private SpanEquipmentAZConnectivityViewEquipmentInfo BuildSpanEquipmentView(GetSpanEquipmentConnectivityView query, SpanEquipment spanEquipment, LookupCollection<SpanEquipmentSpecification> _spanEquipmentSpecifications, LookupCollection<SpanStructureSpecification> _spanStructureSpecifications)
        {
            if (!_spanEquipmentSpecifications.TryGetValue(spanEquipment.SpecificationId, out var spanEquipmentSpecification))
                throw new ApplicationException($"Invalid/corrupted span equipment instance: {spanEquipment.Id} Has reference to non-existing span equipment specification with id: {spanEquipment.SpecificationId}");

            var equipmentData = GatherRelevantSpanEquipmentData(spanEquipment);

            List<TerminalEquipmentAZConnectivityViewTerminalStructureInfo> terminalStructureInfos = new();

            int seqNo = 1;

            List<SpanEquipmentAZConnectivityViewLineInfo> lineInfos = new();

            for (int spanStructureIndex = 1; spanStructureIndex < spanEquipment.SpanStructures.Length; spanStructureIndex++)
            {
                var spanStructure = spanEquipment.SpanStructures[spanStructureIndex];

                var spanStructureSpecification = _spanStructureSpecifications[spanStructure.SpecificationId];

                var spanSegmentToTrace = GetSpanSegmentToTrace(query.RouteNetworkElementId, spanEquipment, spanStructure);

                if (spanEquipment.IsCable)
                {
                    lineInfos.Add(
                        new SpanEquipmentAZConnectivityViewLineInfo(seqNo, equipmentData.GetSpanEquipmentTubeFiberString(spanEquipment, spanStructureIndex), spanSegmentToTrace.Id)
                        {
                            A = GetAEndInfo(equipmentData, spanEquipment, spanSegmentToTrace),
                            Z = GetZEndInfo(equipmentData, spanEquipment, spanSegmentToTrace)
                        }
                    );
                }
                else
                {
                    lineInfos.Add(
                        new SpanEquipmentAZConnectivityViewLineInfo(seqNo, equipmentData.GetSpanStructureConduitString(spanEquipment, spanStructureIndex, spanStructureSpecification), spanSegmentToTrace.Id)
                        {
                            A = GetAEndInfo(equipmentData, spanEquipment, spanSegmentToTrace),
                            Z = GetZEndInfo(equipmentData, spanEquipment, spanSegmentToTrace)
                        }
                    );
                }

                seqNo++;
            }

            return (
                new SpanEquipmentAZConnectivityViewEquipmentInfo(
                       id: spanEquipment.Id,
                       category: spanEquipmentSpecification.Category,
                       name: spanEquipment.Name == null ? "NO NAME" : spanEquipment.Name,
                       isCable: spanEquipment.IsCable,
                       specName: spanEquipmentSpecification.Name,
                       lines: lineInfos.ToArray()
                   )
             );
        }

        private string GetSpanStructureName(SpanEquipment spanEquipment, SpanStructure spanStructure)
        {
            if (!_spanStructureSpecifications.TryGetValue(spanStructure.SpecificationId, out var spanStructureSpecification))
                throw new ApplicationException($"Invalid/corrupted span equipment specification: {spanEquipment.SpecificationId} has reference to non-existing span structure specification with id: {spanStructure.SpecificationId}");

            if (spanEquipment.IsCable)
            {
                return $"Fiber {spanStructure.Position}";
            }
            else
            {
                return $"Subconduit {spanStructure.Position} ({spanStructureSpecification.Color})";
            }

        }

        private SpanSegment GetSpanSegmentToTrace(Guid routeNetworkElementId, SpanEquipment spanEquipment, SpanStructure spanStructure)
        {
            foreach (var spanSegment in spanStructure.SpanSegments)
            {
                if (spanEquipment.NodesOfInterestIds[spanSegment.FromNodeOfInterestIndex] == routeNetworkElementId || spanEquipment.NodesOfInterestIds[spanSegment.ToNodeOfInterestIndex] == routeNetworkElementId)
                    return spanSegment;
            }

            // TODO: use walk of interest to locate correct span segment
            return spanStructure.SpanSegments.First();
            
            //throw new ApplicationException($"Error locating a span segment in span equipment: {spanEquipment.Id} structure position: {spanStructure.Position} that start or ends in route node id: {routeNetworkElementId}");
        }


        private SpanEquipmentAZConnectivityViewEndInfo GetAEndInfo(RelevantEquipmentData relevantEquipmentData, SpanEquipment spanEquipment, SpanSegment spanSegment)
        {
            var traceInfo = relevantEquipmentData.TracedSegments[spanSegment.Id].A;

            return new SpanEquipmentAZConnectivityViewEndInfo()
            {
                ConnectedTo = CreateConnectedToString(relevantEquipmentData, spanEquipment, traceInfo),
                End = CreateEndString(relevantEquipmentData, traceInfo)
            };
        }

        private SpanEquipmentAZConnectivityViewEndInfo GetZEndInfo(RelevantEquipmentData relevantEquipmentData, SpanEquipment spanEquipment, SpanSegment spanSegment)
        {
            var traceInfo = relevantEquipmentData.TracedSegments[spanSegment.Id].Z;

            return new SpanEquipmentAZConnectivityViewEndInfo()
            {
                ConnectedTo = CreateConnectedToString(relevantEquipmentData, spanEquipment, traceInfo),
                End = CreateEndString(relevantEquipmentData, traceInfo)
            };
        }

        private string? CreateConnectedToString(RelevantEquipmentData relevantEquipmentData, SpanEquipment spanEquipment, TraceEndInfo? traceInfo)
        {
            if (traceInfo == null)
                return null;

            var neighborTerminalRef = traceInfo.NeighborTerminal;

            if (neighborTerminalRef.IsDummyEnd)
                return null;

            if (spanEquipment.IsCable)
            {
                var terminalEquipment = neighborTerminalRef.TerminalEquipment(_utilityNetwork);

                var nodeName = relevantEquipmentData.GetNodeOrEquipmentName(neighborTerminalRef.RouteNodeId, terminalEquipment);

                if (nodeName != null)
                    nodeName += " ";

                var terminalStructure = neighborTerminalRef.TerminalStructure(_utilityNetwork);

                var terminal = neighborTerminalRef.Terminal(_utilityNetwork);

                return $"{nodeName}{terminalEquipment.Name}-{terminalStructure.Position}-{terminal.Name}";
            }
            else
            {
                var nodeName = relevantEquipmentData.GetNodeName(neighborTerminalRef.RouteNodeId);

                if (nodeName != null)
                    nodeName += " ";

                return $"{nodeName}";
            }
        }

        private string? CreateEndString(RelevantEquipmentData relevantEquipmentData, TraceEndInfo? traceInfo)
        {
            return traceInfo == null ? null : relevantEquipmentData.GetNodeAndEquipmentEndString(traceInfo.EndTerminal);
        }

        private RelevantEquipmentData GatherRelevantSpanEquipmentData(SpanEquipment spanEquipment)
        {
            var tracedSegments = TraceAllSegments(spanEquipment);

            var endNodesIds = GetEndNodeIdsFromTraceResult(tracedSegments.Values);

            var addressIds = GetAddressIdsFromTraceResult(tracedSegments.Values);

            RelevantEquipmentData relevantEquipmentData = new RelevantEquipmentData(_eventStore, _utilityNetwork, _queryDispatcher, endNodesIds, addressIds);

            relevantEquipmentData.TracedSegments = tracedSegments;

            TryFindAandZ(relevantEquipmentData);

            return relevantEquipmentData;
        }

        private void TryFindAandZ(RelevantEquipmentData relevantEquipmentData)
        {
            foreach (var tracedGraphElement in relevantEquipmentData.TracedSegments.Values)
            {
                // the lower the more A-ish
                int upstreamRank = 0;
                int downstreamRank = 0;

                if (tracedGraphElement.Upstream != null)
                {
                    var endTerminalRouteNode = relevantEquipmentData.RouteNetworkElementById[tracedGraphElement.Upstream.EndTerminal.RouteNodeId];

                    if (endTerminalRouteNode != null && endTerminalRouteNode.RouteNodeInfo != null && endTerminalRouteNode.RouteNodeInfo.Function != null)
                        upstreamRank = (int)endTerminalRouteNode.RouteNodeInfo.Function;
                    else
                        upstreamRank = 1000; // Simple node with no function specificed get the high value (equal low score for A)
                }


                if (tracedGraphElement.Downstream != null)
                {
                    var endTerminalRouteNode = relevantEquipmentData.RouteNetworkElementById[tracedGraphElement.Downstream.EndTerminal.RouteNodeId];

                    if (endTerminalRouteNode != null && endTerminalRouteNode.RouteNodeInfo != null && endTerminalRouteNode.RouteNodeInfo.Function != null)
                        downstreamRank = (int)endTerminalRouteNode.RouteNodeInfo.Function;
                    else
                        downstreamRank = 1000; // Simple node with no function node specified get the high value (equal low score for A)
                }

                if (upstreamRank > downstreamRank)
                    tracedGraphElement.UpstreamIsZ = true;
                else
                    tracedGraphElement.UpstreamIsZ = false;
            }
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

        private HashSet<Guid> GetAddressIdsFromTraceResult(IEnumerable<TraceInfo> traceInfos)
        {
            HashSet<Guid> addressIds = new();

            foreach (var traceInfo in traceInfos)
            {
                foreach (var equipment in GetEndTerminalEquipments(traceInfo))
                {
                    if (equipment.AddressInfo != null)
                    {
                        if (equipment.AddressInfo.UnitAddressId != null && equipment.AddressInfo.UnitAddressId != Guid.Empty)
                            addressIds.Add(equipment.AddressInfo.UnitAddressId.Value);
                        else if (equipment.AddressInfo.AccessAddressId != null && equipment.AddressInfo.AccessAddressId != Guid.Empty)
                            addressIds.Add(equipment.AddressInfo.AccessAddressId.Value);
                    }
                }
            }

            return addressIds;
        }

        private IEnumerable<TerminalEquipment> GetEndTerminalEquipments(TraceInfo traceInfo)
        {
            var result = new HashSet<TerminalEquipment>();

            if (traceInfo.Upstream != null)
            {
                if (!traceInfo.Upstream.EndTerminal.IsDummyEnd)
                {
                    result.Add(traceInfo.Upstream.EndTerminal.TerminalEquipment(_utilityNetwork));
                }
            }

            if (traceInfo.Downstream != null)
            {
                if (!traceInfo.Downstream.EndTerminal.IsDummyEnd)
                {
                    result.Add(traceInfo.Downstream.EndTerminal.TerminalEquipment(_utilityNetwork));
                }
            }

            return result;
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

    
        private class RelevantEquipmentData : RelatedDataHolder
        {
            public Dictionary<Guid, TraceInfo> TracedSegments { get; set; }

            public RelevantEquipmentData(IEventStore eventStore, UtilityNetworkProjection utilityNetwork, IQueryDispatcher queryDispatcher, IEnumerable<Guid> nodeOfInterestIds, HashSet<Guid> addressIds)
               : base(eventStore, utilityNetwork, queryDispatcher, nodeOfInterestIds, addressIds)
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
