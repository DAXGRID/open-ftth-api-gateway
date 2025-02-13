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
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Projections;
using OpenFTTH.UtilityGraphService.Business.Trace.Util;
using OpenFTTH.UtilityGraphService.Business.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.TerminalEquipments.QueryHandling
{
    public class GetTerminalEquipmentConnectivityViewQueryHandler
        : IQueryHandler<GetTerminalEquipmentConnectivityView, Result<TerminalEquipmentAZConnectivityViewModel>>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly UtilityNetworkProjection _utilityNetwork;
        private LookupCollection<RackSpecification> _rackSpecifications;
        private LookupCollection<TerminalStructureSpecification> _terminalStructureSpecifications;
        private LookupCollection<TerminalEquipmentSpecification> _terminalEquipmentSpecifications;

        public GetTerminalEquipmentConnectivityViewQueryHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
        }

        public Task<Result<TerminalEquipmentAZConnectivityViewModel>> HandleAsync(GetTerminalEquipmentConnectivityView query)
        {
            _rackSpecifications = _eventStore.Projections.Get<RackSpecificationsProjection>().Specifications;
            _terminalStructureSpecifications = _eventStore.Projections.Get<TerminalStructureSpecificationsProjection>().Specifications;
            _terminalEquipmentSpecifications = _eventStore.Projections.Get<TerminalEquipmentSpecificationsProjection>().Specifications;

            // If terminal equipment   
            if (_utilityNetwork.TryGetEquipment<TerminalEquipment>(query.terminalEquipmentOrRackId, out var terminalEquipment))
            {
                return Task.FromResult(
                    Result.Ok(BuildStandaloneTerminalEquipmentAZView(query, terminalEquipment))
                );
            }
            else
            {
                var getNodeContainerResult = QueryHelper.GetNodeContainerFromRouteNodeId(_queryDispatcher, query.routeNodeId);

                if (getNodeContainerResult.IsFailed)
                    return Task.FromResult(Result.Fail<TerminalEquipmentAZConnectivityViewModel>(getNodeContainerResult.Errors.First()));

                var nodeContainer = getNodeContainerResult.Value;

                if (nodeContainer == null)
                    throw new ApplicationException("There a bug in QueryHelper.GetNodeContainerFromRouteNodeId query. Cannot just return success and a null node container. Please check.");

                if (nodeContainer.Racks == null || !nodeContainer.Racks.Any(r => r.Id == query.terminalEquipmentOrRackId))
                    return Task.FromResult(Result.Fail<TerminalEquipmentAZConnectivityViewModel>(new TerminalEquipmentError(TerminalEquipmentErrorCodes.RACK_NOT_FOUND, $"Cannot find rack with id: {query.terminalEquipmentOrRackId} in node container with id: {nodeContainer.Id}")));

                return Task.FromResult(
                    Result.Ok(BuildRackWithTerminalEquipmentAZView(query, nodeContainer))
                );
            }
        }

        private TerminalEquipmentAZConnectivityViewModel BuildRackWithTerminalEquipmentAZView(GetTerminalEquipmentConnectivityView query, NodeContainer nodeContainer)
        {
            if (nodeContainer.Racks == null)
                throw new ApplicationException("There a bug in code. Caller must check if rack exists.");

            var rack = nodeContainer.Racks.First(r => r.Id == query.terminalEquipmentOrRackId);

            var rackSpec = _rackSpecifications[rack.SpecificationId];

            TerminalEquipmentAZConnectivityViewNodeStructureInfo rackStructure =
                new TerminalEquipmentAZConnectivityViewNodeStructureInfo(rack.Id, "Rack", rack.Name, rackSpec.Name);

            List<TerminalEquipmentAZConnectivityViewEquipmentInfo> equipmentInfos = new();

            foreach (var mount in rack.SubrackMounts.OrderBy(s => s.Position).Reverse())
            {
                if (_utilityNetwork.TryGetEquipment<TerminalEquipment>(mount.TerminalEquipmentId, out var terminalEquipment))
                {
                    equipmentInfos.Add(BuildTerminalEquipmentView(query, terminalEquipment, rack.Id));
                }
                else
                {
                    throw new ApplicationException($"Cannot find terminal equipment with id: {mount.TerminalEquipmentId} in route node: {query.routeNodeId}");
                }
            }

            return (
                new TerminalEquipmentAZConnectivityViewModel(                    
                    terminalEquipments: equipmentInfos.ToArray()
                )
                {
                    ParentNodeStructures = new TerminalEquipmentAZConnectivityViewNodeStructureInfo[] { rackStructure }
                }
            );
        }

        private TerminalEquipmentAZConnectivityViewModel BuildStandaloneTerminalEquipmentAZView(GetTerminalEquipmentConnectivityView query, TerminalEquipment terminalEquipment)
        {
            return (
                new TerminalEquipmentAZConnectivityViewModel(
                    terminalEquipments: new TerminalEquipmentAZConnectivityViewEquipmentInfo[] {
                       BuildTerminalEquipmentView(query, terminalEquipment)
                    }
                )
            );
        }

        private TerminalEquipmentAZConnectivityViewEquipmentInfo BuildTerminalEquipmentView(GetTerminalEquipmentConnectivityView query, TerminalEquipment terminalEquipment, Guid? parentStructureId = null)
        {
            if (!_terminalEquipmentSpecifications.TryGetValue(terminalEquipment.SpecificationId, out var terminalEquipmentSpecification))
                throw new ApplicationException($"Invalid/corrupted terminal equipment instance: {terminalEquipment.Id} Has reference to non-existing terminal equipment specification with id: {terminalEquipment.SpecificationId}");

            var equipmentData = GatherRelevantTerminalEquipmentData(terminalEquipment);

            List<TerminalEquipmentAZConnectivityViewTerminalStructureInfo> terminalStructureInfos = new();

            foreach (var terminalStructure in GetOrderedTerminalStructures(terminalEquipment))
            {
                if (!_terminalStructureSpecifications.TryGetValue(terminalStructure.SpecificationId, out var terminalStructureSpecification))
                    throw new ApplicationException($"Invalid/corrupted terminal equipment specification: {terminalEquipment.SpecificationId} has reference to non-existing terminal structure specification with id: {terminalStructure.SpecificationId}");

                List<TerminalEquipmentAZConnectivityViewLineInfo> lineInfos = new();

                foreach (var terminal in terminalStructure.Terminals)
                {
                    if (terminal.Direction == TerminalDirectionEnum.BI)
                    {
                        lineInfos.Add(
                            new TerminalEquipmentAZConnectivityViewLineInfo(GetConnectorSymbol(terminal, terminal))
                            {
                                A = GetAEndInfo(equipmentData, terminal),
                                Z = GetZEndInfo(equipmentData, terminal)
                            }
                        );
                    }
                    else if (terminal.Direction == TerminalDirectionEnum.IN)
                    {

                        lineInfos.Add(
                            new TerminalEquipmentAZConnectivityViewLineInfo(GetConnectorSymbol(terminal, terminal))
                            {
                                A = GetAEndInfo(equipmentData, terminal),
                                Z = null
                            }
                        );
                    }
                    else if (terminal.Direction == TerminalDirectionEnum.OUT)
                    {

                        lineInfos.Add(
                            new TerminalEquipmentAZConnectivityViewLineInfo(GetConnectorSymbol(terminal, terminal))
                            {
                                A = null,
                                Z = GetOutEndInfo(equipmentData, terminal)
                            }
                        );
                    }
                }



                terminalStructureInfos.Add(
                    new TerminalEquipmentAZConnectivityViewTerminalStructureInfo(
                        id: terminalStructure.Id,
                        category: terminalStructureSpecification.Category,
                        name: GetTerminalStructureHeaderName(terminalStructure),
                        specName: terminalStructureSpecification.Name,
                        lines: CompactLines(lineInfos).ToArray(),
                        isInterfaceModule: terminalStructureSpecification.IsInterfaceModule
                    )
                );
            }

            return (
                new TerminalEquipmentAZConnectivityViewEquipmentInfo(
                       id: terminalEquipment.Id,
                       category: terminalEquipmentSpecification.Category,
                       name: equipmentData.GetFullEquipmentString(query.routeNodeId, terminalEquipment, true, true),
                       specName: terminalEquipmentSpecification.Name,
                       terminalStructures: terminalStructureInfos.ToArray(),
                       isLineTermination: terminalEquipmentSpecification.IsLineTermination
                   )
                {
                    ParentNodeStructureId = parentStructureId
                }
            );
        }

        private static IEnumerable<TerminalStructure> GetOrderedTerminalStructures(TerminalEquipment terminalEquipment)
        {
            if (terminalEquipment.TerminalStructures.Any(t => t.interfaceInfo != null))
            {
                var interfaces = terminalEquipment.TerminalStructures.Where(t => !t.Deleted).Where(t => t.interfaceInfo != null).OrderBy(t => t.interfaceInfo.SlotNumber).ThenBy(t => t.interfaceInfo.SubSlotNumber).ThenBy(t => t.interfaceInfo.PortNumber);

                var nonInterfaces = terminalEquipment.TerminalStructures.Where(t => !t.Deleted).Where(t => t.interfaceInfo == null).OrderBy(o => o.Position);

                return interfaces.Concat(nonInterfaces);
            }
            else
            {
                return terminalEquipment.TerminalStructures.Where(t => !t.Deleted).OrderBy(o => o.Position);
            }
        }

        private static string GetTerminalStructureHeaderName(TerminalStructure terminalStructure)
        {
            if (terminalStructure.interfaceInfo != null)
                return GetInterfaceName(terminalStructure);
            else                
                return terminalStructure.Name;
        }

        private static string GetInterfaceName(TerminalStructure terminalStructure)
        {
            string interfaceName = terminalStructure.interfaceInfo.InterfaceType + "-" + terminalStructure.interfaceInfo.SlotNumber;

            if (terminalStructure.interfaceInfo.SubSlotNumber > 0)
                interfaceName += ("/" + terminalStructure.interfaceInfo.SubSlotNumber);

            interfaceName += ("/" + terminalStructure.interfaceInfo.PortNumber);

            if (terminalStructure.interfaceInfo.CircuitName != null)
                interfaceName += (" (" + terminalStructure.interfaceInfo.CircuitName + ")");

            return interfaceName;
        }

        private List<TerminalEquipmentAZConnectivityViewLineInfo> CompactLines(List<TerminalEquipmentAZConnectivityViewLineInfo> lineInfos)
        {
            List<TerminalEquipmentAZConnectivityViewLineInfo> compactedLines = new();

            for (int i = 0; i < lineInfos.Count; i++)
            {
                var addLine = true;

                var currentLine = lineInfos[i];

                // If we're not the last
                if (i < lineInfos.Count - 1)
                {
                    var nextLine = lineInfos[i + 1];

                    if (currentLine.A is not null && currentLine.Z is null && nextLine.A is null && nextLine.Z is not null)
                    {
                        // push the A of current line to next line
                        lineInfos[i + 1] = new TerminalEquipmentAZConnectivityViewLineInfo(currentLine.ConnectorSymbol)
                        {
                            A = currentLine.A,
                            Z = nextLine.Z
                        };

                        addLine = false;
                    }
                }

                if (addLine)
                    compactedLines.Add(currentLine);
            }

            return compactedLines;
        }

        private TerminalEquipmentAZConnectivityViewEndInfo GetAEndInfo(RelevantEquipmentData relevantEquipmentData, Terminal terminal)
        {
            var terminalInfo = new TerminalEquipmentAZConnectivityViewTerminalInfo(terminal.Id, terminal.Name);

            var traceInfo = relevantEquipmentData.TracedTerminals[terminal.Id].A;

            FaceKindEnum faceKind = GetAEndFaceKind(relevantEquipmentData, terminal);

            return new TerminalEquipmentAZConnectivityViewEndInfo(terminalInfo, faceKind)
            {
                ConnectedToSpanSegmentId = traceInfo == null ? Guid.Empty : traceInfo.NeighborSegment.Id,
                ConnectedTo = traceInfo == null ? null : CreateConnectedToString(relevantEquipmentData, traceInfo),
                End = traceInfo == null ? null : relevantEquipmentData.GetNodeAndEquipmentEndString(traceInfo.EndTerminal)
            };
        }

        private TerminalEquipmentAZConnectivityViewEndInfo GetZEndInfo(RelevantEquipmentData relevantEquipmentData, Terminal terminal)
        {
            var terminalInfo = new TerminalEquipmentAZConnectivityViewTerminalInfo(terminal.Id, terminal.Name);

            var traceInfo = relevantEquipmentData.TracedTerminals[terminal.Id].Z;

            FaceKindEnum faceKind = GetZEndFaceKind(relevantEquipmentData, terminal);

            return new TerminalEquipmentAZConnectivityViewEndInfo(terminalInfo, faceKind)
            {
                ConnectedToSpanSegmentId = traceInfo == null ? Guid.Empty : traceInfo.NeighborSegment.Id,
                ConnectedTo = traceInfo == null ? null : CreateConnectedToString(relevantEquipmentData, traceInfo),
                End = traceInfo == null ? null : relevantEquipmentData.GetNodeAndEquipmentEndString(traceInfo.EndTerminal)
            };
        }


        private TerminalEquipmentAZConnectivityViewEndInfo GetOutEndInfo(RelevantEquipmentData relevantEquipmentData, Terminal terminal)
        {
            var terminalInfo = new TerminalEquipmentAZConnectivityViewTerminalInfo(terminal.Id, terminal.Name);

            var traceInfo = relevantEquipmentData.TracedTerminals[terminal.Id].Z;

            FaceKindEnum faceKind = GetZEndFaceKind(relevantEquipmentData, terminal);

            return new TerminalEquipmentAZConnectivityViewEndInfo(terminalInfo, faceKind)
            {
                ConnectedToSpanSegmentId = traceInfo == null ? Guid.Empty : traceInfo.NeighborSegment.Id,
                ConnectedTo = traceInfo == null ? null : CreateConnectedToString(relevantEquipmentData, traceInfo),
                End = traceInfo == null ? null : relevantEquipmentData.GetNodeAndEquipmentEndString(traceInfo.EndTerminal)
            };
        }

        private FaceKindEnum GetZEndFaceKind(RelevantEquipmentData relevantEquipmentData, Terminal terminal)
        {
            // If no connector, then it's always a splice no matter if bi teminal or not
            if (terminal.ConnectorType == null)
                return FaceKindEnum.SpliceSide;

            // If terminal is bi-directional, check whether other side is patched or spliced and return opposite of that
            if (terminal.Direction == TerminalDirectionEnum.BI)
            {
                var aEndFaceKind = GetAEndFaceKind(relevantEquipmentData, terminal);

                if (aEndFaceKind == FaceKindEnum.SpliceSide)
                    return FaceKindEnum.PatchSide;
                else
                    return FaceKindEnum.SpliceSide;
            }
            else
            {
                return FaceKindEnum.PatchSide;
            }

        }

        private FaceKindEnum GetAEndFaceKind(RelevantEquipmentData relevantEquipmentData, Terminal terminal)
        {
            if (terminal.ConnectorType == null)
                return FaceKindEnum.SpliceSide;

            // If non bi terminal, and we have a patch connector, then it must be a patch
            if (terminal.Direction != TerminalDirectionEnum.BI)
                return FaceKindEnum.PatchSide;


            bool aConnected = false;
            bool aIsPatched = false;
            bool aIsSpliced = false;
                       

            if (relevantEquipmentData.TracedTerminals[terminal.Id].A != null)
            {
                aConnected = true;

                var a = relevantEquipmentData.TracedTerminals[terminal.Id].A;

                if (a.NeighborSegment is UtilityGraphTerminalToTerminalConnectivityLink)
                {
                    aIsPatched = true;
                }
                else
                {
                    aIsSpliced = true;
                }
            }

            bool zConnected = false;
            bool zIsPatched = false;
            bool zIsSpliced = false;

            if (relevantEquipmentData.TracedTerminals[terminal.Id].Z != null)
            {
                zConnected = true;

                var z = relevantEquipmentData.TracedTerminals[terminal.Id].Z;

                if (z.NeighborSegment is UtilityGraphTerminalToTerminalConnectivityLink)
                {
                    zIsPatched = true;
                }
                else
                {
                    zIsSpliced = true;
                }
            }

            if (!aConnected && !zConnected)
                return FaceKindEnum.PatchSide;

            if (aIsPatched)
                return FaceKindEnum.PatchSide;

            if (zIsSpliced)
                return FaceKindEnum.PatchSide;

            return FaceKindEnum.SpliceSide;
        }

        private string? CreateConnectedToString(RelevantEquipmentData relevantEquipmentData, TraceEndInfo? traceInfo)
        {
            if (traceInfo == null)
                return null;

            if (traceInfo.NeighborSegment is UtilityGraphConnectedSegment)
            {

                var spanEquipment = ((UtilityGraphConnectedSegment)traceInfo.NeighborSegment).SpanEquipment(_utilityNetwork);
                var fiberNo = ((UtilityGraphConnectedSegment)traceInfo.NeighborSegment).StructureIndex;

                return relevantEquipmentData.GetSpanEquipmentFullFiberCableString(spanEquipment, fiberNo);
            }
            else if (traceInfo.NeighborSegment is UtilityGraphTerminalToTerminalConnectivityLink)
            {
                if (traceInfo.NeighborTerminal != null)
                {
                    return relevantEquipmentData.GetEquipmentWithStructureInfoString(traceInfo.NeighborTerminal);
                }
                else
                {
                    return "Error finding end terminal";
                }
            }
            else
            {
                return "Unknown connection type";
            }
        }

        private RelevantEquipmentData GatherRelevantTerminalEquipmentData(TerminalEquipment terminalEquipment)
        {
            var tracedTerminals = TraceAllTerminals(terminalEquipment);

            var endNodesIds = GetEndNodeIdsFromTraceResult(tracedTerminals.Values);

            var addressIds = GetAddressIdsFromTraceResult(tracedTerminals.Values);

            RelevantEquipmentData relevantEquipmentData = new RelevantEquipmentData(_eventStore, _utilityNetwork, _queryDispatcher, endNodesIds, addressIds);

            relevantEquipmentData.TracedTerminals = tracedTerminals;

            TryFindAandZ(relevantEquipmentData);

            return relevantEquipmentData;
        }

        private void TryFindAandZ(RelevantEquipmentData relevantEquipmentData)
        {
            foreach (var tracedTerminal in relevantEquipmentData.TracedTerminals.Values)
            {
                // the lower the more A-ish
                int upstreamRank;
                if (tracedTerminal.Upstream != null)
                {
                    var endTerminalRouteNode = relevantEquipmentData.RouteNetworkElementById[tracedTerminal.Upstream.EndTerminal.RouteNodeId];

                    if (endTerminalRouteNode != null && endTerminalRouteNode.RouteNodeInfo != null && endTerminalRouteNode.RouteNodeInfo.Function != null)
                        upstreamRank = (int)endTerminalRouteNode.RouteNodeInfo.Function;
                    else
                        upstreamRank = 1000; // Simple node with no function specificed get the high value (equal low score for A)
                }
                else
                {
                    // Upstream is the source terminal
                    var endTerminalRouteNode = relevantEquipmentData.RouteNetworkElementById[tracedTerminal.SourceTerminal.RouteNodeId];

                    if (endTerminalRouteNode != null && endTerminalRouteNode.RouteNodeInfo != null && endTerminalRouteNode.RouteNodeInfo.Function != null)
                        upstreamRank = (int)endTerminalRouteNode.RouteNodeInfo.Function;
                    else
                        upstreamRank = 1000; // Simple node with no function specificed get the high value (equal low score for A)
                }


                int downstreamRank;
                if (tracedTerminal.Downstream != null)
                {
                    var endTerminalRouteNode = relevantEquipmentData.RouteNetworkElementById[tracedTerminal.Downstream.EndTerminal.RouteNodeId];

                    if (endTerminalRouteNode != null && endTerminalRouteNode.RouteNodeInfo != null && endTerminalRouteNode.RouteNodeInfo.Function != null)
                        downstreamRank = (int)endTerminalRouteNode.RouteNodeInfo.Function;
                    else
                        downstreamRank = 1000; // Simple node with no function node specified get the high value (equal low score for A)
                }
                else
                {
                    // Downstream is the source terminal
                    var endTerminalRouteNode = relevantEquipmentData.RouteNetworkElementById[tracedTerminal.SourceTerminal.RouteNodeId];

                    if (endTerminalRouteNode != null && endTerminalRouteNode.RouteNodeInfo != null && endTerminalRouteNode.RouteNodeInfo.Function != null)
                        downstreamRank = (int)endTerminalRouteNode.RouteNodeInfo.Function;
                    else
                        downstreamRank = 1000; // Simple node with no function node specified get the high value (equal low score for A)
                }

                if (upstreamRank > downstreamRank)
                    tracedTerminal.UpstreamIsZ = true;
                else
                    tracedTerminal.UpstreamIsZ = false;
            }
        }

        private Dictionary<Guid, TraceInfo> TraceAllTerminals(TerminalEquipment terminalEquipment)
        {
            Dictionary<Guid, TraceInfo> traceInfosByTerminalId = new();

            // Trace all equipment terminals
            foreach (var terminalStructure in terminalEquipment.TerminalStructures.Where(t => !t.Deleted))
            {
                foreach (var terminal in terminalStructure.Terminals)
                {
                    TraceInfo traceInfo = new TraceInfo();

                    var terminalTraceResult = _utilityNetwork.Graph.SimpleTrace(terminal.Id);

                    if (_utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphTerminalRef>(terminal.Id, out var terminalRef))
                    {
                        traceInfo.SourceTerminal = terminalRef;
                    }
                    else
                    {
                        throw new ApplicationException($"Error looking up terminal: {terminal.Id} of terminal equipment: {terminalEquipment.Id} in graph. Graph must should container all terminal equipment terminals!");
                    }

                    if (terminalTraceResult != null)
                    {
                        if (terminalTraceResult.Upstream.Length > 0)
                        {
                            traceInfo.Upstream = GetEndInfoFromTrace(terminal.Id, terminalTraceResult.Upstream);
                        }

                        if (terminalTraceResult.Downstream.Length > 0)
                        {
                            traceInfo.Downstream = GetEndInfoFromTrace(terminal.Id, terminalTraceResult.Downstream);
                        }
                    }

                    traceInfosByTerminalId.Add(terminal.Id, traceInfo);
                }
            }

            return traceInfosByTerminalId;
        }

        private IEnumerable<Guid> GetEndNodeIdsFromTraceResult(IEnumerable<TraceInfo> traceInfos)
        {
            HashSet<Guid> endNodeIds = new();

            foreach (var traceInfo in traceInfos)
            {
                endNodeIds.Add(traceInfo.SourceTerminal.RouteNodeId);
                AddEndNodeIdsToHash(traceInfo, endNodeIds);
            }

            return endNodeIds;
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

        private TraceEndInfo GetEndInfoFromTrace(Guid tracedTerminalId, IGraphObject[] trace)
        {
            if (trace.Length < 2)
                throw new ApplicationException($"Expected trace length to be minimum 2. Please check trace on terminal with id: {tracedTerminalId}");


            // Get neighbor segment
            var neighborSegment = trace.First();

      
            // Get end terminal
            var terminalEnd = trace.Last();

            if (!(terminalEnd is UtilityGraphConnectedTerminal))
                throw new ApplicationException($"Expected end to be a UtilityGraphConnectedTerminal. Please check trace on terminal with id: {tracedTerminalId}");

            if (neighborSegment is UtilityGraphConnectedSegment)
            {
                return new TraceEndInfo((UtilityGraphConnectedSegment)neighborSegment, (UtilityGraphConnectedTerminal)terminalEnd);
            }
            else if (neighborSegment is UtilityGraphTerminalToTerminalConnectivityLink)
            {
                var neighborTerminal = terminalEnd;

                if (trace.Length > 1 && trace[1] is UtilityGraphConnectedTerminal)
                    neighborTerminal = trace[1];

                return new TraceEndInfo((UtilityGraphTerminalToTerminalConnectivityLink)neighborSegment, (UtilityGraphConnectedTerminal)terminalEnd, (UtilityGraphConnectedTerminal)neighborTerminal);
            }
            else
            {
                throw new ApplicationException($"Expected neighbor to be a UtilityGraphConnectedSegment or UtilityGraphTerminalToTerminalConnectivityLink. Please check trace on terminal with id: {tracedTerminalId}");
            }
        }

        private string GetConnectorSymbol(Terminal fromTerminal, Terminal toTerminal)
        {
            string symbolName = "";

            if (fromTerminal.IsSplice)
                symbolName += "Splice";
            else
                symbolName += "Patch";

            if (fromTerminal != toTerminal)
            {
                if (toTerminal.IsSplice)
                    symbolName += "Splice";
                else
                    symbolName += "Patch";
            }

            return symbolName;
        }

        private class RelevantEquipmentData : RelatedDataHolder
        {
            public Dictionary<Guid, TraceInfo> TracedTerminals { get; set; }

            public RelevantEquipmentData(IEventStore eventStore, UtilityNetworkProjection utilityNetwork, IQueryDispatcher queryDispatcher, IEnumerable<Guid> nodeOfInterestIds, HashSet<Guid> addressIds) 
                : base(eventStore, utilityNetwork, queryDispatcher, nodeOfInterestIds, addressIds)
            {
            }
        }



        private record TraceInfo
        {
            public IUtilityGraphTerminalRef SourceTerminal { get; set; }
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
            public GraphEdge NeighborSegment { get; set; }
            public UtilityGraphConnectedTerminal EndTerminal { get; set; }

            public UtilityGraphConnectedTerminal? NeighborTerminal { get; set; }

            public TraceEndInfo(GraphEdge neighborSegment, UtilityGraphConnectedTerminal endTerminal, UtilityGraphConnectedTerminal? neighborTerminal = null)
            {
                NeighborSegment = neighborSegment;
                EndTerminal = endTerminal;
                NeighborTerminal = neighborTerminal;
            }
        }
    }
}
