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
using OpenFTTH.UtilityGraphService.Business.NodeContainers.Projections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.APIGateway.Conversion
{
    public class RackImporter : ImporterBase
    {
        private ILogger<RackImporter> _logger;
        private Guid _workTaskId;
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;
        private UtilityNetworkProjection _utilityNetwork;

        private string _rackTableName = "conversion.racks";
        

        private Dictionary<string, RackSpecification> _rackSpecByName = null;

        public RackImporter(ILogger<RackImporter> logger, Guid workTaskId, IEventStore eventSTore, GeoDatabaseSetting geoDatabaseSettings, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher) : base(geoDatabaseSettings)
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
            _logger.LogInformation("Conversion of racks started...");

            if (!CheckIfConversionTableExists(_rackTableName))
            {
                _logger.LogInformation($"{_rackTableName} don't exists. Will not import racks.");
                return;
            }


            CreateTableLogColumn(_rackTableName);
      
            var racks = LoadDataFromConversionDatabase();

            AddRacksToNetwork(racks.Values.ToList());

            _logger.LogInformation("Conversion of racks finish!");
        }

        private void AddRacksToNetwork(List<RackForConversion> racks)
        {
            using var conn = GetConnection();

            using var logCmd = conn.CreateCommand() as NpgsqlCommand;

            foreach (var rack in racks)
            {
                // Place the rack
                var specId = GetRackSpecificationIdFromName(rack.Specification);

                if (specId != null)
                {
                    var relatedInfo = GetRelatedInformation(rack.NodeId);

                    var placeNodeContainerResult = PlaceRack(logCmd, rack, specId.Value, relatedInfo);

                    if (placeNodeContainerResult.IsSuccess)
                    {
                        LogStatus((NpgsqlCommand)logCmd, _rackTableName, "OK", rack.ExternalId);
                    }
                    else
                    {
                        LogStatus((NpgsqlCommand)logCmd, _rackTableName, placeNodeContainerResult.Errors.First().Message, rack.ExternalId);
                    }
                }
                else
                {
                    LogStatus((NpgsqlCommand)logCmd, _rackTableName, $"Cannot find any rack specification with name: '{rack.Specification}'", rack.ExternalId);
                }
            }
        }
     
        private Result PlaceRack(NpgsqlCommand logCmd, RackForConversion rack, Guid specId, RelatedEquipmentInfo relatedInfo)
        {
            Guid correlationId = Guid.NewGuid();

            var commandUserContext = new UserContext("conversion", _workTaskId)
            {
                EditingRouteNodeId = rack.NodeId
            };

            // Check if node container already exists
            if (relatedInfo.NodeContainer == null)
            {
                return Result.Fail(new Error($"No container exists in node: {rack.NodeId}"));
            }

            var rackSpecId = GetRackSpecificationIdFromName(rack.Specification);

            if (rackSpecId == null)
                return Result.Fail(new Error($"Cannot find rack specfication: {rack.Specification}"));

            // Place rack
            var placeRackCmd = new PlaceRackInNodeContainer(
               correlationId,
               commandUserContext,
               relatedInfo.NodeContainer.Id,
               rack.RackId,
               rackSpecId.Value,
               rack.Name,
               rack.HeightInUnits
           );

            var placeRackResult = _commandDispatcher.HandleAsync<PlaceRackInNodeContainer, Result>(placeRackCmd).Result;


            LogStatus((NpgsqlCommand)logCmd, _rackTableName, "external_id", rack.ExternalId, placeRackResult);

            return placeRackResult;
        }

        private Guid? GetRackSpecificationIdFromName(string specificationName)
        {
            if (_rackSpecByName == null)
            {
                _rackSpecByName = new Dictionary<string, RackSpecification>();

                foreach (var nodeSpec in _eventStore.Projections.Get<RackSpecificationsProjection>().Specifications)
                {
                    if (!_rackSpecByName.ContainsKey(nodeSpec.Name.ToLower()))
                        _rackSpecByName[nodeSpec.Name.ToLower()] = nodeSpec;
                }
            }

            var key = specificationName.Trim().ToLower();

            if (_rackSpecByName.ContainsKey(key))
                return _rackSpecByName[key].Id;

            return null;
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
                        result.NodeContainer = equipmentQueryResult.Value.NodeContainers.First();

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

        private Dictionary<Guid, RackForConversion> LoadDataFromConversionDatabase()
        {
            var nodeContainersForConversions = LoadRacksToBeImportedFromDatabase();

            return nodeContainersForConversions;
        }

        private Dictionary<Guid, RackForConversion> LoadRacksToBeImportedFromDatabase()
        {
            Dictionary<Guid, RackForConversion> nodeContainersForConversions = new();

            using var dbConn = GetConnection();

            // Load node containers
            using var nodeContainerSelectCmd = dbConn.CreateCommand();
            nodeContainerSelectCmd.CommandText = "SELECT external_id, external_spec, route_node_id, rack_id, specification,name, number_of_units FROM " + _rackTableName + " WHERE status is null ORDER BY external_id";

            using var rackReader = nodeContainerSelectCmd.ExecuteReader();

            while (rackReader.Read())
            {
                var rack = new RackForConversion();

                rack.ExternalId = rackReader.GetString(0).Trim();
                var externalSpec = rackReader.GetString(1).Trim();
                rack.NodeId = Guid.Parse(rackReader.GetString(2));
                rack.RackId = Guid.Parse(rackReader.GetString(3));
                rack.Specification = rackReader.GetString(4).Trim();
                rack.Name = rackReader.GetString(5).Trim();
                rack.HeightInUnits = Int32.Parse(rackReader.GetString(6).Trim());

                nodeContainersForConversions.Add(rack.RackId, rack);
            }

            dbConn.Close();

            return nodeContainersForConversions;
        }

        private class RackForConversion
        {
            public string ExternalId { get; set; }
            public string Specification { get; set; }
            public Guid NodeId { get; set; }
            public Guid RackId { get; set; }
            public string Name { get; set; }
            public int HeightInUnits { get; set; }
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

