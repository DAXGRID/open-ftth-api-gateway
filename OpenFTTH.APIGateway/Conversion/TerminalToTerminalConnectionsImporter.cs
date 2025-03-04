using OpenFTTH.Results;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenFTTH.APIGateway.Settings;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.Business.Graph;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.APIGateway.Conversion
{
    public class TerminalToTerminalConnectionsImporter : ImporterBase
    {
        private ILogger<TerminalToTerminalConnectionsImporter> _logger;
        private Guid _workTaskId;
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;
        private UtilityNetworkProjection _utilityNetwork;

        private string _terminalToTerminalConnectionsTableName = "conversion.terminal_to_terminal_connection";

        public TerminalToTerminalConnectionsImporter(ILogger<TerminalToTerminalConnectionsImporter> logger, Guid workTaskId, IEventStore eventSTore, GeoDatabaseSetting geoDatabaseSettings, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher) : base(geoDatabaseSettings)
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
            _logger.LogInformation("Conversion of terminal to terminal connections started...");

            if (!CheckIfConversionTableExists(_terminalToTerminalConnectionsTableName))
            {
                _logger.LogInformation($"{_terminalToTerminalConnectionsTableName} don't exists. Will not import terminal to terminal connections.");
                return;
            }

            CreateTableLogColumn(_terminalToTerminalConnectionsTableName);

            var connectiondToImport = LoadDataFromConversionDatabase();

            AddTerminalToTerminalConnectionToNetwork(connectiondToImport);

            _logger.LogInformation("Conversion of terminal to terminal connections finish!");
        }

        private void AddTerminalToTerminalConnectionToNetwork(List<ConnectionForConversion> connections)
        {
            using var conn = GetConnection();

            using var logCmd = conn.CreateCommand() as NpgsqlCommand;

            foreach (var connection in connections)
            {
                var relatedInfo = GetRelatedInformation(connection.NodeId);

                var addConnectionResult = AddConnection(logCmd, connection, relatedInfo);

                if (addConnectionResult.IsSuccess)
                {
                    LogStatus((NpgsqlCommand)logCmd, _terminalToTerminalConnectionsTableName, "OK", connection.ExternalId);
                }
                else
                {
                    LogStatus((NpgsqlCommand)logCmd, _terminalToTerminalConnectionsTableName, addConnectionResult.Errors.First().Message, connection.ExternalId);
                }

            }

        }

        private Result AddConnection(NpgsqlCommand logCmd, ConnectionForConversion connection, RelatedEquipmentInfo relatedInfo)
        {

            Guid correlationId = Guid.NewGuid();

            var commandUserContext = new UserContext("conversion", _workTaskId)
            {
                EditingRouteNodeId = connection.NodeId
            };

            try
            {

                if (connection.To != null && connection.To.Contains("Fiber"))
                {
                    // Find from terminal
                    string[] fromSplit = connection.From.Split('-');
                    var fromRackName = fromSplit[0];
                    var fromEquipmentName = fromSplit[1];
                    var fromStructurePosition = Int32.Parse(fromSplit[2]);
                    var fromPort = fromSplit[3];

                    var fromTerminalId = FindTerminal(relatedInfo, fromRackName, fromEquipmentName, fromStructurePosition, fromPort);

                    if (fromTerminalId == null)
                        return Result.Fail(new Error($"Cannot find terminal by from information: '{connection.From}"));


                    // get flexhus id
                    string[] toFlexhusSplit = connection.To.Split('@');
                    Guid flexHusId = Guid.Parse(toFlexhusSplit[1]);

                    // fl
                    string[] activeFlexSplit = toFlexhusSplit[0].Split(';');


                    // Find to terminal
                    string[] toSplit = activeFlexSplit[0].Split('-');
                    var toRackName = toSplit[0];
                    var toEquipmentName = toSplit[1];
                    var toStructurePosition = Int32.Parse(toSplit[2]);
                    var toFiber = Int32.Parse(toSplit[3].Replace("Fiber ", ""));

                    var fromOdfTerminalId = FindTerminalByFiber(relatedInfo, toRackName, toEquipmentName, toStructurePosition, toFiber);

                    if (fromOdfTerminalId == null)
                        return Result.Fail(new Error($"Cannot find terminal by from information: '{connection.To}"));

                    // Find other end of fiber
                    var traceResult = _utilityNetwork.Graph.SimpleTrace(fromOdfTerminalId.Value);

                    Terminal? downstreamOdfTerminal = null;

                    if (traceResult.Downstream != null && traceResult.Downstream.Length > 0)
                    {
                        downstreamOdfTerminal = ((UtilityGraphConnectedTerminal)traceResult.Downstream.Last()).Terminal(_utilityNetwork);
                    }
                    else if (traceResult.Upstream != null && traceResult.Upstream.Length > 0)
                    {
                        downstreamOdfTerminal = ((UtilityGraphConnectedTerminal)traceResult.Upstream.Last()).Terminal(_utilityNetwork);
                    }

                    if (downstreamOdfTerminal == null)
                        return Result.Fail(new Error($"Cannot find downstream ODF terminal by tracing to ODF from information: '{connection.To}"));


                    // Connect from WDM to ODF in active node

                    // Do the connect between terminals)
                    var activeConnectCmd = new ConnectTerminalsAtRouteNode(
                        correlationId: Guid.NewGuid(),
                        userContext: commandUserContext,
                        routeNodeId: connection.NodeId,
                        fromTerminalId: fromTerminalId.Value,
                        toTerminalId: fromOdfTerminalId.Value,
                        fiberCoordLength: 0
                    );
                    var activeConnectCmdResult = _commandDispatcher.HandleAsync<ConnectTerminalsAtRouteNode, Result>(activeConnectCmd).Result;

                    if (activeConnectCmdResult.IsFailed)
                        return activeConnectCmdResult;


                    // Do the connect in flexhus between ODF and 1:2 split

                    var relatedFlexhusInfo = GetRelatedInformation(flexHusId);

                    // Find to splitter terminal
                    string[] toSplitOdfSplit = activeFlexSplit[1].Split('-');
                    var toSplitOdfRackName = toSplitOdfSplit[0];
                    var toSplitEquipmentName = toSplitOdfSplit[1];
                    var toSplitStructurePosition = Int32.Parse(toSplitOdfSplit[2]);
                    var toSplitPort = toSplitOdfSplit[3];

                    var toSplitId = FindTerminal(relatedFlexhusInfo, toSplitOdfRackName, toSplitEquipmentName, toSplitStructurePosition, toSplitPort);

                    if (toSplitId == null)
                        return Result.Fail(new Error($"Cannot find 1:2 splitter in port in flexhus from information: '{connection.To}"));



                    // Do the connect between terminals)
                    var flexhusConnectCmd = new ConnectTerminalsAtRouteNode(
                        correlationId: Guid.NewGuid(),
                        userContext: commandUserContext,
                        routeNodeId: flexHusId,
                        fromTerminalId: downstreamOdfTerminal.Id,
                        toTerminalId: toSplitId.Value,
                        fiberCoordLength: 0
                    );
                    var flexhusConnectCmdResult = _commandDispatcher.HandleAsync<ConnectTerminalsAtRouteNode, Result>(flexhusConnectCmd).Result;

                    if (flexhusConnectCmdResult.IsFailed)
                        return activeConnectCmdResult;



                    return Result.Ok();


                }
                else // normal connection
                {
                    // Find from terminal
                    string[] fromSplit = connection.From.Split(';');
                    var fromRackName = fromSplit[0];
                    var fromEquipmentName = fromSplit[1];
                    var fromStructurePosition = Int32.Parse(fromSplit[2]);
                    var fromPort = fromSplit[3];

                    var fromTerminalId = FindTerminal(relatedInfo, fromRackName, fromEquipmentName, fromStructurePosition, fromPort);

                    if (fromTerminalId == null)
                        return Result.Fail(new Error($"Cannot find to terminal from information: '{connection.From}"));

                    // Find to terminal
                    string[] toSplit = connection.To.Split(';');
                    var toRackName = toSplit[0];
                    var toEquipmentName = toSplit[1];
                    var toStructurePosition = Int32.Parse(toSplit[2]);
                    var toPort = toSplit[3];

                    var toTerminalId = FindTerminal(relatedInfo, toRackName, toEquipmentName, toStructurePosition, toPort);

                    if (toTerminalId == null)
                        return Result.Fail(new Error($"Cannot find to terminal fom information: '{connection.To}"));
                                 
                    // Do the connect between terminals)
                    var connectCmd = new ConnectTerminalsAtRouteNode(
                        correlationId: Guid.NewGuid(),
                        userContext: commandUserContext,
                        routeNodeId: connection.NodeId,
                        fromTerminalId: fromTerminalId.Value,
                        toTerminalId: toTerminalId.Value,
                        fiberCoordLength: 0
                    );
                    var connectCmdResult = _commandDispatcher.HandleAsync<ConnectTerminalsAtRouteNode, Result>(connectCmd).Result;


                    LogStatus((NpgsqlCommand)logCmd, _terminalToTerminalConnectionsTableName, "external_id", connection.ExternalId, connectCmdResult);

                    return connectCmdResult;

                }

            }
            catch (Exception ex)
            {
                return Result.Fail(new Error($"Exception doing terminal to terminal connection: from='{connection.From}' to='{connection.To} Error: {ex.Message}"));
            }
        }

        private Guid? FindTerminal(RelatedEquipmentInfo relatedInfo, string rackName, string equipmentName, int structurePosition, string portName)
        {
            if (relatedInfo.NodeContainer == null)
                return null;
          
            // Find terminal equipment
            TerminalEquipment? te = null;

            if (!String.IsNullOrEmpty(rackName))
            {
                if (relatedInfo.NodeContainer.Racks == null)
                    return null;

                if (!relatedInfo.NodeContainer.Racks.Any(r => r.Name == rackName))
                    return null;

                var rack = relatedInfo.NodeContainer.Racks.First(r => r.Name == rackName);

                foreach (var subRack in rack.SubrackMounts)
                {
                    if (_utilityNetwork.TryGetEquipment<TerminalEquipment>(subRack.TerminalEquipmentId, out var terminalEquipment))
                    {
                        if (terminalEquipment.NamingInfo != null && terminalEquipment.NamingInfo.Name == equipmentName)
                            te = terminalEquipment;
                    }
                }
            }
            else
            {
                if (relatedInfo.TerminalEquipmentByName.ContainsKey(equipmentName))
                    te = relatedInfo.TerminalEquipmentByName[equipmentName];
            }


            if (te == null)
                return null;


            // Find terminal structure
            if (!te.TerminalStructures.Any(s => s.Position == structurePosition))
                return null;

            var terminalStructure = te.TerminalStructures.First(s => s.Position == structurePosition);

            // Find terminal
            if (terminalStructure.Terminals.Any(t => t.Name == portName))
                return terminalStructure.Terminals.First(t => t.Name == portName).Id;
            else if (terminalStructure.Terminals.Any(t => t.Name == portName.ToLower()))
                return terminalStructure.Terminals.First(t => t.Name == portName.ToLower()).Id;
            else if (terminalStructure.Terminals.Any(t => t.Name == portName.ToLower().Replace(" ", "")))
                return terminalStructure.Terminals.First(t => t.Name == portName.ToLower().Replace(" ", "")).Id;
            else
                return null;

        }

        private Guid? FindTerminalByFiber(RelatedEquipmentInfo relatedInfo, string rackName, string equipmentName, int structurePosition, int fiberNo)
        {
            if (relatedInfo.NodeContainer == null)
                return null;
                   
            // Find terminal equipment
            TerminalEquipment? te = null;


            if (!String.IsNullOrEmpty(rackName))
            {
                if (relatedInfo.NodeContainer.Racks == null)
                    return null;

                if (!relatedInfo.NodeContainer.Racks.Any(r => r.Name == rackName))
                    return null;

                var rack = relatedInfo.NodeContainer.Racks.First(r => r.Name == rackName);

                foreach (var subRack in rack.SubrackMounts)
                {
                    if (_utilityNetwork.TryGetEquipment<TerminalEquipment>(subRack.TerminalEquipmentId, out var terminalEquipment))
                    {
                        if (terminalEquipment.NamingInfo != null && terminalEquipment.NamingInfo.Name == equipmentName)
                            te = terminalEquipment;
                    }
                }
            }
            else
            {
                if (relatedInfo.TerminalEquipmentByName.ContainsKey(equipmentName))
                    te = relatedInfo.TerminalEquipmentByName[equipmentName];
            }

            if (te == null)
                return null;

            // Find terminal structure
            if (!te.TerminalStructures.Any(s => s.Position == structurePosition))
                return null;

            var terminalStructure = te.TerminalStructures.First(s => s.Position == structurePosition);


            var version = _utilityNetwork.Graph.LatestCommitedVersion;

            // Find terminal by fiber
            foreach (var terminal in terminalStructure.Terminals)
            {
                if (_utilityNetwork.Graph.TryGetGraphElement<UtilityGraphConnectedTerminal>(terminal.Id, out var terminalGraphElement))
                {
                    foreach (var neighborSegment in terminalGraphElement.NeighborElements(version))
                    {
                        if (neighborSegment is UtilityGraphConnectedSegment)
                        {
                            var segment = (UtilityGraphConnectedSegment)neighborSegment;

                            if (segment.StructureIndex == fiberNo)
                                return terminal.Id;
                        }
                    }
                }
            }

            return null;
        }


        private List<ConnectionForConversion> LoadDataFromConversionDatabase()
        {
            var connectionsForConversions = LoadConnectionsToBeImportedFromDatabase();

            return connectionsForConversions;
        }

        private List<ConnectionForConversion> LoadConnectionsToBeImportedFromDatabase()
        {
            List<ConnectionForConversion> connectionsForConversions = new();

            using var dbConn = GetConnection();

            // Load connections
            using var connectionsSelectCmd = dbConn.CreateCommand();
            connectionsSelectCmd.CommandText = "SELECT external_id, route_node_id, from_eq, to_eq FROM " + _terminalToTerminalConnectionsTableName + " WHERE status is null ORDER BY external_id";

            using var connectionstReader = connectionsSelectCmd.ExecuteReader();

            while (connectionstReader.Read())
            {
                var connection = new ConnectionForConversion();

                connection.ExternalId = connectionstReader.GetString(0).Trim();
                connection.NodeId = Guid.Parse(connectionstReader.GetString(1));
                connection.From = connectionstReader.GetString(2);
                connection.To = connectionstReader.GetString(3);
              
                connectionsForConversions.Add(connection);
            }

            dbConn.Close();

            return connectionsForConversions;
        }

        private RelatedEquipmentInfo GetRelatedInformation(Guid routeNodeId)
        {

            RelatedEquipmentInfo result = new();

            // Get interest information from existing span equipment
            var interestQueryResult = _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(new GetRouteNetworkDetails(new RouteNetworkElementIdList() { routeNodeId }) { RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementOnly }).Result;

            if (interestQueryResult.IsSuccess)
            {
                InterestIdList interestIdsToFetch = new InterestIdList();

                foreach (var interestRel in interestQueryResult.Value.RouteNetworkElements.First().InterestRelations)
                {
                    interestIdsToFetch.Add(interestRel.RefId);
                }

                if (interestIdsToFetch.Count == 0)
                    return result;

                var equipmentQueryResult = _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(new GetEquipmentDetails(interestIdsToFetch)).Result;

                if (equipmentQueryResult.IsSuccess)
                {
                    Dictionary<Guid, SpanEquipmentWithRelatedInfo> spanEquipmentByInterestId = equipmentQueryResult.Value.SpanEquipment.ToDictionary(s => s.WalkOfInterestId);

                    if (equipmentQueryResult.Value.NodeContainers != null && equipmentQueryResult.Value.NodeContainers.Count > 0)
                    {
                        result.NodeContainer = equipmentQueryResult.Value.NodeContainers.First();

                        if (result.NodeContainer.TerminalEquipmentReferences != null)
                        {
                            foreach (var teId in result.NodeContainer.TerminalEquipmentReferences)
                            {
                                if (_utilityNetwork.TryGetEquipment<TerminalEquipment>(teId, out var te))
                                {
                                    if (!result.TerminalEquipmentByName.ContainsKey(te.Name))
                                    {
                                        result.TerminalEquipmentByName.Add(te.Name, te);
                                    }
                                }
                            }
                        }
                    }

                    foreach (var interestRel in interestQueryResult.Value.RouteNetworkElements.First().InterestRelations)
                    {
                        if (interestRel.RelationKind == RouteNetworkInterestRelationKindEnum.Start)
                        {
                            var spanEq = spanEquipmentByInterestId[interestRel.RefId];
                            result.OutgoingSpanEquipments.Add(spanEq);
                            result.SpanEquipmentById.Add(spanEq.Id, spanEq);
                        }
                        else if (interestRel.RelationKind == RouteNetworkInterestRelationKindEnum.End)
                        {
                            var spanEq = spanEquipmentByInterestId[interestRel.RefId];
                            result.IngoingSpanEquipments.Add(spanEq);
                            result.SpanEquipmentById.Add(spanEq.Id, spanEq);
                        }
                    }

                    

                }
                else
                    _logger.LogError($"Error querying equipment details in route node with id: {routeNodeId} " + equipmentQueryResult.Errors.First().Message);
            }
            else
                _logger.LogError($"Error querying interests related to route node with id: {routeNodeId} " + interestQueryResult.Errors.First().Message);

            return result;
        }

        private class ConnectionForConversion
        {
            public string ExternalId { get; set; }
            public Guid NodeId { get; set; }
            public string From { get; set; }
            public string To { get; set; }

        }

        private class RelatedEquipmentInfo
        {
            public List<SpanEquipment> IngoingSpanEquipments = new();
            public List<SpanEquipment> OutgoingSpanEquipments = new();
            public Dictionary<Guid, SpanEquipment> SpanEquipmentById = new();
            public Dictionary<string, TerminalEquipment> TerminalEquipmentByName = new();

            public bool SingleConduitsOnly
            {
                get
                {
                    foreach (var spanEquipment in IngoingSpanEquipments)
                    {
                        if (spanEquipment.SpanStructures.Length > 1)
                            return false;
                    }

                    foreach (var spanEquipment in OutgoingSpanEquipments)
                    {
                        if (spanEquipment.SpanStructures.Length > 1)
                            return false;
                    }

                    return true;
                }
            }

            public NodeContainer NodeContainer { get; internal set; }
        }
    }
}

