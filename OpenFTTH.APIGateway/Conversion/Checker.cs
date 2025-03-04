using DAX.ObjectVersioning.Graph;
using OpenFTTH.Results;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.Settings;
using OpenFTTH.APIGateway.Util;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Projections;
using System;
using System.IO;
using System.Linq;

namespace OpenFTTH.APIGateway.Conversion
{
    public class Checker : ImporterBase
    {
        private ILogger<Checker> _logger;
        private Guid _workTaskId;
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;
        private UtilityNetworkProjection _utilityNetwork;


        public Checker(ILogger<Checker> logger, Guid workTaskId, IEventStore eventSTore, GeoDatabaseSetting geoDatabaseSettings, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher) : base(geoDatabaseSettings)
        {
            _logger = logger;
            _workTaskId = workTaskId;
            _eventStore = eventSTore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;

            _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
        }


        public void Run()
        {
            TraceAllFlexSplitterPorts();
            TraceAllInstallations();
        }

        public void TraceAllInstallations()
        {
            _logger.LogInformation("Installation checker started...");

            var terminalEquipmentSpecifications = _eventStore.Projections.Get<TerminalEquipmentSpecificationsProjection>().Specifications;

            var terminalEquipments = _utilityNetwork.TerminalEquipmentByEquipmentId.Values;

            using StreamWriter csvFile = new("c:/temp/openftth_kunde_check.csv");

            var csvHeader = "\"instnr\";\"rammer_rack\";\"rammer_eq\";\"rammer_kort\";\"rammer_port\"";
            csvFile.WriteLine(csvHeader);

            foreach (var terminalEquipment in terminalEquipments)
            {
                var spec = terminalEquipmentSpecifications[terminalEquipment.SpecificationId];

                if (spec.Name == "Kundeterminering")
                {
                    string eqName = "";
                    string eqKort = "";
                    string eqPort = "";
                    string endsInRack = "nej";

                    for (int i = 0; i < 4; i++)
                    {
                        // trace terminal
                        var traceResult = _utilityNetwork.Graph.SimpleTrace(terminalEquipment.TerminalStructures[0].Terminals[i].Id);


                        UtilityGraphConnectedTerminal? endTerminal = null;

                        if (CheckIfEndTerminalIsWithinRackEquipment(terminalEquipmentSpecifications, traceResult.Upstream))
                        {
                            endTerminal = (UtilityGraphConnectedTerminal)traceResult.Upstream.Last();
                            endsInRack = "ja";
                        }
                        else if (CheckIfEndTerminalIsWithinRackEquipment(terminalEquipmentSpecifications, traceResult.Downstream))
                        {
                            endTerminal = (UtilityGraphConnectedTerminal)traceResult.Downstream.Last();
                            endsInRack = "ja";
                        }


                        if (endTerminal != null)
                        {
                            var terminal = endTerminal.Terminal(_utilityNetwork);
                            var structure = endTerminal.TerminalStructure(_utilityNetwork);
                            var equipment = endTerminal.TerminalEquipment(_utilityNetwork);

                            eqName = equipment.Name;
                            eqKort = structure.Name;
                            eqPort = terminal.Name;
                        }

                        // try another port if we have not reaced olt
                        if (!(eqName.ToLower().Contains("olt") || eqName.ToLower().Contains("pon02")) && i < 3)
                            continue;


                        var csvLine = "\"" + terminalEquipment.Name + "\";\"" + endsInRack + "\";\"" + eqName + "\";\"" + eqKort + "\";\"" + eqPort + "\"";

                        csvFile.WriteLine(csvLine);

                        break;
                    }
                }
            }

            csvFile.Close();

            _logger.LogInformation("Installation checker finish!");
        }

        public void TraceAllFlexSplitterPorts()
        {
            _logger.LogInformation("Splitter checker started...");

            var terminalEquipmentSpecifications = _eventStore.Projections.Get<TerminalEquipmentSpecificationsProjection>().Specifications;

            var terminalEquipments = _utilityNetwork.TerminalEquipmentByEquipmentId.Values;

            using StreamWriter csvFile = new("c:/temp/openftth_splitter_check.csv");

            var csvHeader = "\"teknikhus\";\"splitter_nr\";\"splitter_port\";\"rammer_udstyr\";\"rammer_kort\";\"rammer_port\";\"rammer_cpe\"";
            csvFile.WriteLine(csvHeader);

            foreach (var terminalEquipment in terminalEquipments)
            {
                var spec = terminalEquipmentSpecifications[terminalEquipment.SpecificationId];

                if (spec.Name.ToLower().Contains("splitterholder"))
                {
                    var nodeEquipmentResult = QueryHelper.GetNodeContainer(_queryDispatcher, terminalEquipment.NodeContainerId);

                    if (nodeEquipmentResult.IsFailed)
                    {
                        continue;
                    }

                    var routeNodeId = nodeEquipmentResult.Value.RouteNodeId;

                    var routeNodeQueryResult = _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
                        new GetRouteNetworkDetails(new OpenFTTH.RouteNetwork.API.Model.RouteNetworkElementIdList() { routeNodeId })
                        {
                            RouteNetworkElementFilter = new RouteNetworkElementFilterOptions() { IncludeNamingInfo = true }
                        }
                        ).Result;

                    var routeNodeName = routeNodeQueryResult.Value.RouteNetworkElements[routeNodeId].Name;


                    foreach (var terminalStructure in terminalEquipment.TerminalStructures)
                    {
                        string splitterNr = terminalStructure.Position.ToString();

                        bool firstPort = true;
                        foreach (var splitterPort in terminalStructure.Terminals)
                        {
                            string eqName = "";
                            string eqKort = "";
                            string eqPort = "";
                            string rammerCpe = "nej";

                            if (!firstPort)
                            {
                                // trace port
                                var traceResult = _utilityNetwork.Graph.SimpleTrace(splitterPort.Id);

                                UtilityGraphConnectedTerminal? endTerminal = null;

                                if (traceResult != null && traceResult.Downstream.Length > 0)
                                {

                                    endTerminal = (UtilityGraphConnectedTerminal)traceResult.Downstream.Last();

                                    if (endTerminal != null && endTerminal.TerminalEquipmentId != Guid.Empty)
                                    {
                                        var terminal = endTerminal.Terminal(_utilityNetwork);
                                        var structure = endTerminal.TerminalStructure(_utilityNetwork);
                                        var equipment = endTerminal.TerminalEquipment(_utilityNetwork);

                                        eqName = equipment.Name;
                                        eqKort = structure.Name;
                                        eqPort = terminal.Name;

                                        var endTerminalSpec = terminalEquipmentSpecifications[equipment.SpecificationId];

                                        if (endTerminalSpec.IsCustomerTermination)
                                            rammerCpe = "ja";
                                    }
                                }


                                var csvLine = "\"" + routeNodeName + "\";\"" + splitterNr + "\";\"" +  splitterPort.Name + "\";\"" + eqName + "\";\"" + eqKort + "\";\"" + eqPort + "\";\""  + rammerCpe + "\"";

                                csvFile.WriteLine(csvLine);
                            }

                            firstPort = false;

                        }
                    }
                }
            }

            csvFile.Close();

            _logger.LogInformation("Installation checker finish!");
        }


        private bool CheckIfEndTerminalIsWithinRackEquipment(LookupCollection<TerminalEquipmentSpecification> terminalEquipmentSpecifications, IGraphObject[] graphObjects)
        {
            if (graphObjects.Length > 0)
            {
                var upstreamTerminal = graphObjects.Last();

                if (upstreamTerminal != null)
                {
                    if (_utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphTerminalRef>(upstreamTerminal.Id, out var terminalRef))
                    {
                        if (!terminalRef.IsDummyEnd)
                        {
                            var terminalEquipment = terminalRef.TerminalEquipment(_utilityNetwork);

                            var spec = terminalEquipmentSpecifications[terminalEquipment.SpecificationId];

                            if (spec.IsRackEquipment)
                                return true;
                        }
                    }
                }
            }

            return false;
        }


    }
}

