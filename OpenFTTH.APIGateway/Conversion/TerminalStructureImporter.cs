using OpenFTTH.Results;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenFTTH.APIGateway.Settings;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Projections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.APIGateway.Conversion
{
    public class TerminalStructureImporter : ImporterBase
    {
        private ILogger<TerminalStructureImporter> _logger;
        private Guid _workTaskId;
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;
        private UtilityNetworkProjection _utilityNetwork;

        private string _terminalConnectionTable = "conversion.terminal_structure";

        private Dictionary<string, TerminalStructureSpecification> _terminalStructureSpecByName = null;


        public TerminalStructureImporter(ILogger<TerminalStructureImporter> logger, Guid workTaskId, IEventStore eventSTore, GeoDatabaseSetting geoDatabaseSettings, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher) : base(geoDatabaseSettings)
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
            _logger.LogInformation("Conversion of terminal structure(s) started...");

            if (!CheckIfConversionTableExists(_terminalConnectionTable))
            {
                _logger.LogInformation($"{_terminalConnectionTable} don't exists. Will not import terminal equipments.");
                return;
            }


            CreateTableLogColumn(_terminalConnectionTable);

            var terminalEquipments = LoadDataFromConversionDatabase();

            AddTerminalStructures(terminalEquipments);

            _logger.LogInformation("Conversion of terminal structures(s) finish!");
        }

        private void AddTerminalStructures(List<TerminalStructureForConversion> terminalStructures)
        {
            using var conn = GetConnection();

            using var logCmd = conn.CreateCommand() as NpgsqlCommand;

            foreach (var terminalStructure in terminalStructures)
            {
                var placeNodeContainerResult = PlaceTerminalStructure(logCmd, terminalStructure);

                if (placeNodeContainerResult.IsSuccess)
                {
                    LogStatus((NpgsqlCommand)logCmd, _terminalConnectionTable, "OK", terminalStructure.ExternalId);
                }
                else
                {
                    LogStatus((NpgsqlCommand)logCmd, _terminalConnectionTable, placeNodeContainerResult.Errors.First().Message, terminalStructure.ExternalId);
                }

            }
        }

        private Result PlaceTerminalStructure(NpgsqlCommand logCmd, TerminalStructureForConversion terminalStructure)
        {
            Guid correlationId = Guid.NewGuid();

            var commandUserContext = new UserContext("conversion", _workTaskId)
            {
                EditingRouteNodeId = terminalStructure.NodeId
            };

            var terminalStructureSpecificationId = GetTerminalStructureSpecificationIdFromName(terminalStructure.Specification);

            if (terminalStructureSpecificationId == null)
                return Result.Fail(new Error($"Cannot find terminal structure specfication: {terminalStructure.Specification}"));



            var placeEqCmd = new PlaceAdditionalStructuresInTerminalEquipment(
                  correlationId: Guid.NewGuid(),
                  userContext: commandUserContext,
                  routeNodeId: terminalStructure.NodeId,
                  terminalEquipmentId: terminalStructure.TerminalEquipmentId,
                  structureSpecificationId: terminalStructureSpecificationId.Value,
                  position: terminalStructure.Position,
                  numberOfStructures: 1
            );

            try
            {
                var placeEqResult = _commandDispatcher.HandleAsync<PlaceAdditionalStructuresInTerminalEquipment, Result>(placeEqCmd).Result;
                
                LogStatus((NpgsqlCommand)logCmd, _terminalConnectionTable, "external_id", terminalStructure.ExternalId, placeEqResult);

                return placeEqResult;
            }
            catch (Exception ex)
            {
                return Result.Fail(new Error($"Exception: {ex.Message}"));
            }
        }

        private List<TerminalStructureForConversion> LoadDataFromConversionDatabase()
        {
            var objectsForConversions = LoadTerminalEquipmentsToBeImportedFromDatabase();

            return objectsForConversions;
        }

        private List<TerminalStructureForConversion> LoadTerminalEquipmentsToBeImportedFromDatabase()
        {
            List<TerminalStructureForConversion> termianEquipmentsForConversions = new();

            using var dbConn = GetConnection();

            // Load terminal equipments
            using var terminalEquipmentSelectCmd = dbConn.CreateCommand();
            terminalEquipmentSelectCmd.CommandText = "SELECT external_id, route_node_id, terminal_equipment_id, specification, position FROM " + _terminalConnectionTable + " WHERE status is null ORDER BY external_id";

            using var terminalEquipmentReader = terminalEquipmentSelectCmd.ExecuteReader();

            while (terminalEquipmentReader.Read())
            {
                var terminalEquipment = new TerminalStructureForConversion();

                var externalSpec = terminalEquipmentReader.GetString(1).Trim();

                terminalEquipment.ExternalId = terminalEquipmentReader.GetString(0).Trim();
                terminalEquipment.NodeId = Guid.Parse(terminalEquipmentReader.GetString(1));
                terminalEquipment.TerminalEquipmentId = Guid.Parse(terminalEquipmentReader.GetString(2));
                terminalEquipment.Specification = terminalEquipmentReader.GetString(3).Trim();
                terminalEquipment.Position = Int32.Parse(terminalEquipmentReader.GetString(4).Trim());
          
                termianEquipmentsForConversions.Add(terminalEquipment);
            }

            dbConn.Close();

            return termianEquipmentsForConversions;
        }

        private Guid? GetTerminalStructureSpecificationIdFromName(string specificationName)
        {
            if (_terminalStructureSpecByName == null)
            {
                _terminalStructureSpecByName = new Dictionary<string, TerminalStructureSpecification>();

                var specs = _eventStore.Projections.Get<TerminalStructureSpecificationsProjection>().Specifications.ToList();

                foreach (var teSpec in specs)
                {
                    if (!_terminalStructureSpecByName.ContainsKey(teSpec.Name.ToLower()))
                        _terminalStructureSpecByName[teSpec.Name.ToLower()] = teSpec;
                }
            }

            var key = specificationName.Trim().ToLower();

            if (_terminalStructureSpecByName.ContainsKey(key))
                return _terminalStructureSpecByName[key].Id;

            return null;
        }

        private class TerminalStructureForConversion
        {
            public string ExternalId { get; set; }
            public Guid NodeId { get; set; }
            public Guid TerminalEquipmentId { get; set; }
            public string Specification { get; set; }
            public int Position { get; set; }
     
        }

      
    }
}

