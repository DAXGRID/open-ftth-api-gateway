using Marten.Linq.SoftDeletes;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using Newtonsoft.Json.Linq;
using OpenFTTH.Events.RouteNetwork.Infos;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.Business.Interest.Projections;
using OpenFTTH.RouteNetwork.Business.RouteElements.Model;
using OpenFTTH.RouteNetwork.Business.RouteElements.StateHandling;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.NodeContainers.Projections;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Projections;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace OpenFTTH.APIGateway.Reporting
{
    public class CustomerTerminationReport
    {
        private ILogger<CustomerTerminationReport> _logger;
        private IEventStore _eventStore;
        private UtilityNetworkProjection _utilityNetwork;
        private IRouteNetworkState _routeNetworkState;

        public CustomerTerminationReport(ILogger<CustomerTerminationReport> logger, IEventStore eventSTore, IRouteNetworkState routeNetworkState)
        {
            _logger = logger;
            _eventStore = eventSTore;
            _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
            _routeNetworkState = routeNetworkState;
        }

        public IEnumerable<string> TraceAllCustomerTerminations()
        {
            _logger.LogInformation("Service terminations trace started...");
            var interestsProjection = _eventStore.Projections.Get<InterestsProjection>();

            var terminalEquipmentSpecifications = _eventStore.Projections.Get<TerminalEquipmentSpecificationsProjection>().Specifications;

            var terminalEquipments = _utilityNetwork.TerminalEquipmentByEquipmentId.Values;

            var firstLineTrace = true;

            foreach (var terminalEquipment in terminalEquipments)
            {
                var equipmentSpec = terminalEquipmentSpecifications[terminalEquipment.SpecificationId];

                if (equipmentSpec.IsCustomerTermination && terminalEquipment.TerminalStructures.Where(t => !t.Deleted).Count() > 0)
                {
                    var sourceTerminalStructure = terminalEquipment.TerminalStructures.First(t => !t.Deleted);

                    Console.Out.WriteLine(terminalEquipment.Name);

                    for (int i = 0; i < sourceTerminalStructure.Terminals.Count(); i++)
                    {
                        // trace terminal
                        var sourceTerminal = sourceTerminalStructure.Terminals[i];

                        var traceResult = _utilityNetwork.Graph.SimpleTrace(sourceTerminal.Id);

                        // Collect installation information
                        var traceLine = new InstallationTraceResultLine();
                        traceLine.inst_name = terminalEquipment.Name;
                        traceLine.inst_card = sourceTerminalStructure.Name;
                        traceLine.inst_port = sourceTerminal.Name;
                        traceLine.inst_spec = equipmentSpec.Name;

                        var traceState = new TraceState();

                        if (traceResult.Upstream != null && traceResult.Upstream.Length > 0)
                        {
                            var currentRouteNodeId = Guid.Empty;

                            TraceHopContext previousHopContext = null;
                            TraceHopContext currentHopContext = null;
                            TraceHopContext firstHopInNodeContext = null;

                            int nodeCount = 0;

                            double totalLength = 0;

                            foreach (var hop in traceResult.Upstream)
                            {
                                if (hop is UtilityGraphConnectedTerminal && !((UtilityGraphConnectedTerminal)hop).IsDummyEnd)
                                {
                                    currentHopContext = new TraceHopContext((UtilityGraphConnectedTerminal)hop, _utilityNetwork, interestsProjection, _routeNetworkState, _eventStore);

                                    if (currentHopContext.IsFirstHopInNode(previousHopContext))
                                    {
                                        nodeCount++;
                                        firstHopInNodeContext = currentHopContext;
                                    }


                                    // If we reach a stand alone splice closure, then it's a secondary splice point
                                    if (traceLine.sd1_node_spec == null && nodeCount < 3 && currentHopContext.nodeContainerSpec.Category == "SpliceClosure")
                                    {
                                        traceLine.sd1_node_name = currentHopContext.GetNodeName();
                                        traceLine.sd1_node_spec = currentHopContext.nodeContainerSpec.Name;

                                        traceLine.sd1_1sp_spec = firstHopInNodeContext.hopEquipmentSpec.Name;
                                        traceLine.sd1_1sp_name = firstHopInNodeContext.hopEquipment.Name;
                                        traceLine.sd1_1sp_card = firstHopInNodeContext.GetCardName();
                                        traceLine.sd1_1sp_port = firstHopInNodeContext.hopTerminal.Name;
                                    }

                                    // If we did not reach a flexpoint yet, and it's not a standalone splice clousure, then it must be the primary distribution point
                                    if (traceLine.pd_node_spec == null &&
                                        traceLine.fp_node_spec == null &&
                                        currentHopContext.nodeContainerSpec.Category != "SpliceClosure"
                                    )
                                    {
                                        traceLine.pd_node_name = currentHopContext.GetNodeName();
                                        traceLine.pd_node_spec = currentHopContext.nodeContainerSpec.Name;

                                        traceLine.pd_1sp_spec = firstHopInNodeContext.hopEquipmentSpec.Name;
                                        traceLine.pd_1sp_name = firstHopInNodeContext.hopEquipment.Name;
                                        traceLine.pd_1sp_card = firstHopInNodeContext.GetCardName();
                                        traceLine.pd_1sp_port = firstHopInNodeContext.hopTerminal.Name;
                                    }

                                    if (traceLine.fp_node_spec == null && currentHopContext.IsFlexPoint)
                                    {
                                        traceLine.fp_node_name = currentHopContext.GetNodeName();
                                        traceLine.fp_node_spec = currentHopContext.nodeContainerSpec.Name;

                                        traceLine.fp_1sp_spec = firstHopInNodeContext.hopEquipmentSpec.Name;
                                        traceLine.fp_1sp_name = firstHopInNodeContext.hopEquipment.Name;
                                        traceLine.fp_1sp_card = firstHopInNodeContext.GetCardName();
                                        traceLine.fp_1sp_port = firstHopInNodeContext.hopTerminal.Name;
                                        traceLine.fp_1sp_rack = firstHopInNodeContext.GetRackName();
                                    }

                                    if (currentHopContext.IsCustomerSplitter)
                                    {
                                        traceLine.fp_node_name = currentHopContext.GetNodeName();
                                        traceLine.fp_node_spec = currentHopContext.nodeContainerSpec.Name;

                                        traceLine.fp_1sp_spec = firstHopInNodeContext.hopEquipmentSpec.Name;
                                        traceLine.fp_1sp_name = firstHopInNodeContext.hopEquipment.Name;
                                        traceLine.fp_1sp_card = firstHopInNodeContext.GetCardName();
                                        traceLine.fp_1sp_port = firstHopInNodeContext.hopTerminal.Name;
                                        traceLine.fp_1sp_rack = firstHopInNodeContext.GetRackName();

                                        traceLine.fp_splitter_spec = currentHopContext.hopTerminalStructureSpec.Name;
                                        traceLine.fp_splitter_port = currentHopContext.hopTerminal.Name;
                                        traceLine.fp_splitter_name = currentHopContext.hopTerminalStructure.Name;
                                        traceLine.fp_splitter_rack = currentHopContext.GetRackName();
                                    }

                                    if (traceLine.co_1sp_spec == null && currentHopContext.IsCentralOffice)
                                    {
                                        traceLine.co_node_name = currentHopContext.GetNodeName();
                                        traceLine.co_node_spec = currentHopContext.nodeContainerSpec.Name;

                                        traceLine.co_1sp_spec = firstHopInNodeContext.hopEquipmentSpec.Name;
                                        traceLine.co_1sp_name = firstHopInNodeContext.hopEquipment.Name;
                                        traceLine.co_1sp_card = firstHopInNodeContext.GetCardName();
                                        traceLine.co_1sp_port = firstHopInNodeContext.hopTerminal.Name;
                                        traceLine.co_1sp_rack = firstHopInNodeContext.GetRackName();
                                    }

                                    if (currentHopContext.IsLineTermination)
                                    {
                                        traceLine.co_node_name = currentHopContext.GetNodeName();
                                        traceLine.co_node_spec = currentHopContext.nodeContainerSpec.Name;

                                        traceLine.co_1sp_spec = firstHopInNodeContext.hopEquipmentSpec.Name;
                                        traceLine.co_1sp_name = firstHopInNodeContext.hopEquipment.Name;
                                        traceLine.co_1sp_card = firstHopInNodeContext.GetCardName();
                                        traceLine.co_1sp_port = firstHopInNodeContext.hopTerminal.Name;
                                        traceLine.co_1sp_rack = firstHopInNodeContext.GetRackName();

                                        traceLine.co_olt_spec = currentHopContext.hopEquipmentSpec.Name;
                                        traceLine.co_olt_name = currentHopContext.hopEquipment.Name;
                                        traceLine.co_olt_card = currentHopContext.hopTerminalStructure.Name;
                                        traceLine.co_olt_port = currentHopContext.hopTerminal.Name;
                                        traceLine.co_olt_rack = currentHopContext.GetRackName();
                                    }

                                    previousHopContext = currentHopContext;
                                }
                                else if (hop is UtilityGraphConnectedSegment)
                                {
                                    var segment = hop as UtilityGraphConnectedSegment;
                                    var spanEquipment = segment.SpanEquipment(_utilityNetwork);

                                    double length = GetSpanEquipmentLength(interestsProjection, spanEquipment);
                                    totalLength += length;
                                }
                            }

                            // We do this to make sure that the CSV header is written first.
                            if (firstLineTrace)
                            {
                                yield return GetCsvHeaderFromTrace(traceLine);
                                firstLineTrace = false;
                            }


                            yield return GetCsvLineFromTrace(traceLine, totalLength);
                        }
                    }
                }
            }

            _logger.LogInformation("Service terminations trace finish!");
        }

        private double GetSpanEquipmentLength(InterestsProjection interestProjection, SpanEquipment spanEquipment)
        {
            double length = 0;

            var routeNetworkElements = new ValidatedRouteNetworkWalk(interestProjection.GetInterest(spanEquipment.WalkOfInterestId).Value.RouteNetworkElementRefs);

            foreach (var segmentId in routeNetworkElements.SegmentIds)
            {
                var routeSegment = _routeNetworkState.GetRouteNetworkElement(segmentId);

                if (routeSegment.Coordinates != null)
                    length += GetLineStringLength(routeSegment.Coordinates);

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


        private string GetCsvHeaderFromTrace(InstallationTraceResultLine line)
        {
            var myType = line.GetType();

            var csvHeader = "";

            foreach (PropertyInfo prop in myType.GetProperties().Reverse())
            {
                if (csvHeader != "")
                    csvHeader += ";";

                csvHeader += $"\"{prop.Name}\"";
            }

            csvHeader += ";\"length\"";

            return csvHeader;
        }

        private string GetCsvLineFromTrace(InstallationTraceResultLine line, double totalLength)
        {
            var myType = line.GetType();

            var first = true;
            var csvLine = "";
            foreach (PropertyInfo prop in myType.GetProperties().Reverse())
            {
                if (!first)
                {
                    csvLine += ";";
                }

                object propValue = prop.GetValue(line, null);

                if (propValue != null)
                {
                    csvLine += $"\"{propValue.ToString()}\"";
                }

                first = false;
            }

            csvLine += $";{Math.Round(totalLength).ToString("F0")}";

            return csvLine;
        }
    }

    public class TraceHopContext
    {
        public Terminal hopTerminal { get; init; }
        public TerminalStructure hopTerminalStructure { get; init; }
        public TerminalStructureSpecification hopTerminalStructureSpec { get; init; }
        public TerminalEquipment hopEquipment { get; init; }
        public TerminalEquipmentSpecification hopEquipmentSpec { get; init; }
        public NodeContainer nodeContainer { get; init; }
        public NodeContainerSpecification nodeContainerSpec { get; init; }

        private UtilityNetworkProjection _utilityNetworkProjection;
        private static LookupCollection<NodeContainerSpecification> _nodeContainerSpecifications;
        private static LookupCollection<TerminalEquipmentSpecification> _terminalEquipmentSpecifications;
        private static LookupCollection<TerminalStructureSpecification> _terminalStructureSpecifications;
        private RouteNode routeNode;

        public TraceHopContext(UtilityGraphConnectedTerminal hopTerminalRef, UtilityNetworkProjection utilityNetworkProjection, InterestsProjection interestsProjection, IRouteNetworkState routeNetworkState, IEventStore eventStore)
        {
            _utilityNetworkProjection = utilityNetworkProjection;

            if (_nodeContainerSpecifications == null)
                _nodeContainerSpecifications = eventStore.Projections.Get<NodeContainerSpecificationsProjection>().Specifications;

            if (_terminalEquipmentSpecifications == null)
                _terminalEquipmentSpecifications = eventStore.Projections.Get<TerminalEquipmentSpecificationsProjection>().Specifications;

            if (_terminalStructureSpecifications == null)
                _terminalStructureSpecifications = eventStore.Projections.Get<TerminalStructureSpecificationsProjection>().Specifications;

            this.hopTerminal = hopTerminalRef.Terminal(utilityNetworkProjection);
            this.hopTerminalStructure = hopTerminalRef.TerminalStructure(utilityNetworkProjection);
            this.hopTerminalStructureSpec = _terminalStructureSpecifications[hopTerminalStructure.SpecificationId];

            this.hopEquipment = hopTerminalRef.TerminalEquipment(utilityNetworkProjection);
            this.hopEquipmentSpec = _terminalEquipmentSpecifications[hopEquipment.SpecificationId];
            utilityNetworkProjection.TryGetEquipment(hopEquipment.NodeContainerId, out NodeContainer aNodeContainer);
            this.nodeContainer = aNodeContainer;
            this.nodeContainerSpec = _nodeContainerSpecifications[nodeContainer.SpecificationId];

            var interestQueryResult = interestsProjection.GetInterest(nodeContainer.InterestId);
            this.routeNode = routeNetworkState.GetRouteNetworkElement(interestQueryResult.Value.RouteNetworkElementRefs.First()) as RouteNode;
        }

        public bool IsLineTermination
        {
            get
            {
                if (hopEquipmentSpec.IsLineTermination)
                    return true;
                else
                    return false;
            }
        }

        public bool IsCentralOffice
        {
            get
            {
                if (nodeContainer != null && nodeContainer.Racks != null)
                {
                    foreach (var rack in nodeContainer.Racks)
                    {
                        foreach (var rackMount in rack.SubrackMounts)
                        {
                            _utilityNetworkProjection.TryGetEquipment<TerminalEquipment>(rackMount.TerminalEquipmentId, out TerminalEquipment terminalEquipment);

                            if (terminalEquipment != null)
                            {
                                if (_terminalEquipmentSpecifications[terminalEquipment.SpecificationId].IsLineTermination)
                                    return true;
                            }
                        }
                    }
                }

                return false;
            }
        }

        public bool IsFlexPoint
        {
            get
            {
                if (routeNode.RouteNodeInfo != null && (routeNode.RouteNodeInfo.Kind == RouteNodeKindEnum.CentralOfficeSmall || routeNode.RouteNodeInfo.Kind == RouteNodeKindEnum.CentralOfficeMedium || routeNode.RouteNodeInfo.Kind == RouteNodeKindEnum.CentralOfficeBig))
                {
                    return true;
                }

                if (nodeContainer != null && nodeContainer.Racks != null)
                {
                    foreach (var rack in nodeContainer.Racks)
                    {
                        foreach (var rackMount in rack.SubrackMounts)
                        {
                            _utilityNetworkProjection.TryGetEquipment<TerminalEquipment>(rackMount.TerminalEquipmentId, out TerminalEquipment terminalEquipment);

                            if (terminalEquipment != null)
                            {
                                foreach (var terminalStructure in terminalEquipment.TerminalStructures)
                                {
                                    if (_terminalStructureSpecifications[terminalStructure.SpecificationId].IsCustomerSplitter)
                                        return true;
                                }
                            }
                        }
                    }
                }

                return false;
            }
        }

        public bool IsCustomerSplitter
        {
            get
            {
                if (hopTerminalStructureSpec.IsCustomerSplitter)
                    return true;
                else
                    return false;
            }
        }

        public string GetCardName()
        {
            if (hopEquipmentSpec.Name != null && hopEquipmentSpec.Name.ToLower().Contains("lisa"))
                return hopEquipment.Name;
            else
                return hopTerminalStructure.Name;
        }

        public string GetRackName()
        {
            if (nodeContainer != null && nodeContainer.Racks != null)
            {
                foreach (var rack in nodeContainer.Racks)
                {
                    foreach (var rackMount in rack.SubrackMounts)
                    {
                        if (hopEquipment.Id == rackMount.TerminalEquipmentId)
                            return rack.Name;
                    }
                }
            }

            return null;
        }

        public bool IsFirstHopInNode(TraceHopContext previousHopContext)
        {
            if (previousHopContext == null)
                return true;

            if (this.nodeContainer.RouteNodeId != previousHopContext.nodeContainer.RouteNodeId)
                return true;
            else
                return false;
        }

        internal string GetNodeName()
        {
            if (routeNode.NamingInfo != null)
                return routeNode.NamingInfo.Name;
            else
                return null;
        }
    }

    public class TraceState
    {
        public TraceLevel TraceLevel { get; set; }
        public List<string> TraceStagesReached = new();
        public NodeContainer PreviousNodeContainer { get; set; }

        public bool IsStageActive(string stage)
        {
            if (TraceStagesReached.Contains(stage) && !TraceStagesReached.Contains(stage + "-"))
                return true;
            else
                return false;
        }
    }

    public enum TraceLevel
    {
        SecondarySplicePoint1 = 1,
        SecondarySplicePoint2 = 2,
        SecondarySplicePoint3 = 3,
        PrimarySplicePoint = 4,
        FlexPoint = 5,
        CentralOffice = 6
    }

    public class InstallationTraceResultLine
    {
        // If trace hits OLT, fill in the following
        public string co_olt_port { get; set; }
        public string co_olt_card { get; set; }
        public string co_olt_name { get; set; }
        public string co_olt_spec { get; set; }
        public string co_olt_rack { get; set; }

        // First splice/patch point as trace frame in the technical house
        public string co_1sp_port { get; set; }
        public string co_1sp_card { get; set; }
        public string co_1sp_spec { get; set; }

        public string co_1sp_name { get; set; }
        public string co_1sp_rack { get; set; }
        public string co_node_spec { get; set; }
        public string co_node_name { get; set; }

        // Flex point information
        // If trace hits flex point:
        public string fp_splitter_port { get; set; }
        public string fp_splitter_name { get; set; }
        public string fp_splitter_rack { get; set; }
        public string fp_splitter_spec { get; set; }

        // First splice/patch point as trace frame in the flex point
        public string fp_1sp_port { get; set; }
        public string fp_1sp_card { get; set; }
        public string fp_1sp_spec { get; set; }
        public string fp_1sp_name { get; set; }
        public string fp_1sp_rack { get; set; }
        public string fp_node_spec { get; set; }
        public string fp_node_name { get; set; }

        // Primary distribution point/street cabinet information
        public string pd_1sp_port { get; set; }
        public string pd_1sp_card { get; set; }
        public string pd_1sp_name { get; set; }
        public string pd_1sp_spec { get; set; }
        public string pd_node_spec { get; set; }
        public string pd_node_name { get; set; }

        // Secondary splice point 1
        public string sd1_1sp_port { get; set; }
        public string sd1_1sp_card { get; set; }
        public string sd1_1sp_name { get; set; }
        public string sd1_1sp_spec { get; set; }
        public string sd1_node_spec { get; set; }
        public string sd1_node_name { get; set; }

        // Customer termination
        // The trace starts at the customer termination (port 1 to 4)
        public string inst_port { get; set; }
        public string inst_card { get; set; }
        public string inst_spec { get; set; }
        public string inst_name { get; set; }
    }
}

