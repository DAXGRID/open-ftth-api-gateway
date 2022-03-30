using FluentResults;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenFTTH.APIGateway.Settings;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.APIGateway.Conversion
{
    public class SegmentToTerminalConnectionsImporter : ImporterBase
    {
        private ILogger<SegmentToTerminalConnectionsImporter> _logger;
        private Guid _workTaskId;
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;
        private UtilityNetworkProjection _utilityNetwork;

        private string _segmentToTerminalConnectionsTableName = "conversion.segment_to_terminal_connections";

        public SegmentToTerminalConnectionsImporter(ILogger<SegmentToTerminalConnectionsImporter> logger, Guid workTaskId, IEventStore eventSTore, GeoDatabaseSetting geoDatabaseSettings, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher) : base(geoDatabaseSettings)
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
            _logger.LogInformation("Conversion of span to terminal connections started...");

            if (!CheckIfConversionTableExists(_segmentToTerminalConnectionsTableName))
            {
                _logger.LogInformation($"{_segmentToTerminalConnectionsTableName} don't exists. Will not import span to terminal connections.");
                return;
            }

            CreateTableLogColumn(_segmentToTerminalConnectionsTableName);

            var connectiondToImport = LoadDataFromConversionDatabase();

            AddSegmentToTerminalConnectionToNetwork(connectiondToImport);

            _logger.LogInformation("Conversion of span to terminal connections finish!");
        }

        private void AddSegmentToTerminalConnectionToNetwork(List<ConnectionForConversion> connections)
        {
            using var conn = GetConnection();

            using var logCmd = conn.CreateCommand() as NpgsqlCommand;

            foreach (var connection in connections)
            {

                var addConnectionResult = AddConnection(logCmd, connection);

                if (addConnectionResult.IsSuccess)
                {
                    LogStatus((NpgsqlCommand)logCmd, _segmentToTerminalConnectionsTableName, "OK", connection.ExternalId);
                }
                else
                {
                    LogStatus((NpgsqlCommand)logCmd, _segmentToTerminalConnectionsTableName, addConnectionResult.Errors.First().Message, connection.ExternalId);
                }

            }

        }

        private Result AddConnection(NpgsqlCommand logCmd, ConnectionForConversion connection)
        {

            Guid correlationId = Guid.NewGuid();

            var commandUserContext = new UserContext("conversion", _workTaskId)
            {
                EditingRouteNodeId = connection.NodeId
            };

            if (!_utilityNetwork.TryGetEquipment<SpanEquipment>(connection.SpanEquipmentId, out var spanEquipment))
                return Result.Fail(new Error($"Connect find span equipment with id: {connection.SpanEquipmentId}"));


            if (!_utilityNetwork.TryGetEquipment<TerminalEquipment>(connection.TerminalEquipmentId, out var terminalEquipment))
                return Result.Fail(new Error($"Connect find termian equipment with id: {connection.TerminalEquipmentId}"));


            // Find span equipment ids
            List<ConnectSpanSegmentToTerminalOperation> connects = new();

            int terminalIndexPos = 0;

            foreach (var spanIndex in connection.SpanEquipmentIndexes)
            {
                if (spanEquipment.SpanStructures.Length > spanIndex)
                {
                    var spanSegmentId = spanEquipment.SpanStructures[spanIndex].SpanSegments[0].Id;

                    var terminalIndex = connection.TerminalEquipmentIndexes[terminalIndexPos];

                    if (terminalEquipment.TerminalStructures.Length > terminalIndex.Item1)
                    {
                        var structure = terminalEquipment.TerminalStructures[terminalIndex.Item1];

                        if (structure.Terminals.Length > terminalIndex.Item2)
                        {
                            var terminalId = structure.Terminals[terminalIndex.Item2].Id;

                            connects.Add(new ConnectSpanSegmentToTerminalOperation(spanSegmentId, terminalId));
                        }
                        else
                            System.Diagnostics.Debug.WriteLine($"Terminal equipment {terminalEquipment.Id} has no terminal at structure {terminalIndex.Item1} terminal index {terminalIndex.Item2}");
                    }
                    else
                        System.Diagnostics.Debug.WriteLine($"Terminal equipment {terminalEquipment.Id} has no structure at index {terminalIndex.Item1}");

                }
                else
                    System.Diagnostics.Debug.WriteLine($"Span equipment {spanEquipment.Id} has no structure at index {spanIndex}");


                terminalIndexPos++;
            }



            // ACT (do the connect between cable and equipment)
            var connectCmd = new ConnectSpanSegmentsWithTerminalsAtRouteNode(
                correlationId: Guid.NewGuid(),
                userContext: commandUserContext,
                routeNodeId: connection.NodeId,
                connects: connects.ToArray()
            );
            var connectCmdResult = _commandDispatcher.HandleAsync<ConnectSpanSegmentsWithTerminalsAtRouteNode, Result>(connectCmd).Result;


            LogStatus((NpgsqlCommand)logCmd, _segmentToTerminalConnectionsTableName, "external_id", connection.ExternalId, connectCmdResult);

            return connectCmdResult;
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
            connectionsSelectCmd.CommandText = "SELECT external_id, route_node_id, span_equipment_id, span_segment_indexes, terminal_equipment_id,  terminal_indexes FROM " + _segmentToTerminalConnectionsTableName + " WHERE status is null ORDER BY external_id";

            using var connectionstReader = connectionsSelectCmd.ExecuteReader();

            while (connectionstReader.Read())
            {
                var connection = new ConnectionForConversion();

                connection.ExternalId = connectionstReader.GetString(0).Trim();
                connection.NodeId = Guid.Parse(connectionstReader.GetString(1));
                connection.SpanEquipmentId = Guid.Parse(connectionstReader.GetString(2));
                connection.SpanEquipmentIndexes = connectionstReader.GetString(3).Split(',').Select(x => Int32.Parse(x)).ToArray();
                connection.TerminalEquipmentId = Guid.Parse(connectionstReader.GetString(4));

                List<(int, int)> terminalIndexes = new();

                var idSplit = connectionstReader.GetString(5).Split(',');

                foreach (var id in idSplit)
                {
                    var indexSplit = id.Split('-');
                    terminalIndexes.Add((Int32.Parse(indexSplit[0]) - 1, Int32.Parse(indexSplit[1]) - 1));
                }

                connection.TerminalEquipmentIndexes = terminalIndexes.ToArray();

                connectionsForConversions.Add(connection);
            }

            dbConn.Close();

            return connectionsForConversions;
        }

        private class ConnectionForConversion
        {
            public string ExternalId { get; set; }
            public Guid NodeId { get; set; }
            public Guid SpanEquipmentId { get; set; }
            public int[] SpanEquipmentIndexes { get; set; }
            public Guid TerminalEquipmentId { get; set; }
            public (int, int)[] TerminalEquipmentIndexes { get; set; }
        }

        private class RelatedEquipmentInfo
        {
            public List<SpanEquipment> IngoingSpanEquipments = new();
            public List<SpanEquipment> OutgoingSpanEquipments = new();
            public Dictionary<Guid, SpanEquipment> SpanEquipmentById = new();

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

