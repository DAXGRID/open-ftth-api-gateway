using OpenFTTH.Results;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenFTTH.APIGateway.Settings;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.NodeContainers.Projections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.APIGateway.Conversion
{
    public class ConnectivityImporter : ImporterBase
    {
        private ILogger<ConnectivityImporter> _logger;
        private Guid _workTaskId;
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;

        private string _tableName = "conversion.ne_connectivity_result";

        private Dictionary<string, NodeContainerSpecification> _nodeContainerSpecByName = null;

        public ConnectivityImporter(ILogger<ConnectivityImporter> logger, Guid workTaskId, IEventStore eventSTore, GeoDatabaseSetting geoDatabaseSettings, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher) : base(geoDatabaseSettings)
        {
            _logger = logger;
            _workTaskId = workTaskId;
            _eventStore = eventSTore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
        }


        public void Run()
        {
            _logger.LogInformation("Conversion of connectivity started...");

            CreateTableLogColumn(_tableName);

            var nodeContainers = LoadDataFromConversionDatabase(_tableName);

            AddContainersToNetwork(nodeContainers);

            _logger.LogInformation("Conversion of connectivity finish!");
        }

        private void AddContainersToNetwork(List<NodeContainerForConversion> nodeContainers)
        {
            using var conn = GetConnection();

            using var logCmd = conn.CreateCommand();

            foreach (var nodeContainer in nodeContainers)
            {

                var specId = GetNodeContainerSpecificationIdFromName(nodeContainer.Specification);

                if (specId != null)
                {
                    var result = PlaceNodeContainer(nodeContainer, specId.Value);

                    if (result.IsFailed)
                    {
                        LogStatus((NpgsqlCommand)logCmd, _tableName, result.Errors.First().Message, nodeContainer.ExternalId);
                    }
                    else
                    {
                        LogStatus((NpgsqlCommand)logCmd, _tableName, "OK", nodeContainer.ExternalId);
                    }
                }
                else
                {
                    LogStatus((NpgsqlCommand)logCmd, _tableName, $"Cannot find any node container specification with description: '{nodeContainer.Specification}'", nodeContainer.ExternalId);
                }
            }
        }

        private Result PlaceNodeContainer(NodeContainerForConversion nodeContainer, Guid specId)
        {
            var routeNodeId = Guid.NewGuid();
            var nodeContainerId = Guid.NewGuid();

            Guid correlationId = Guid.NewGuid();

            var commandUserContext = new UserContext("conversion", _workTaskId)
            {
                EditingRouteNodeId = routeNodeId
            };

            // First register the node of interest where to place the node container
            var nodeOfInterestId = Guid.NewGuid();

            var registerNodeOfInterestCommand = new RegisterNodeOfInterest(correlationId, commandUserContext, nodeOfInterestId, routeNodeId);

            var registerNodeOfInterestCommandResult = _commandDispatcher.HandleAsync<RegisterNodeOfInterest, Result<RouteNetworkInterest>>(registerNodeOfInterestCommand).Result;

            if (registerNodeOfInterestCommandResult.IsFailed)
            {
                return registerNodeOfInterestCommandResult;
            }

            // Now place the node container in the walk
            var placeNodeContainerCommand = new PlaceNodeContainerInRouteNetwork(correlationId, commandUserContext, nodeContainerId, specId, registerNodeOfInterestCommandResult.Value)
            {
                ManufacturerId = null,
                LifecycleInfo = new LifecycleInfo(DeploymentStateEnum.InService, null, null)
            };

            var placeNodeContainerResult = _commandDispatcher.HandleAsync<PlaceNodeContainerInRouteNetwork, Result>(placeNodeContainerCommand).Result;

            // Unregister interest if place node container failed
            if (placeNodeContainerResult.IsFailed)
            {
                var unregisterCommandResult = _commandDispatcher.HandleAsync<UnregisterInterest, Result>(new UnregisterInterest(correlationId, commandUserContext, nodeOfInterestId)).Result;

                if (unregisterCommandResult.IsFailed)
                    return unregisterCommandResult;
            }

            return placeNodeContainerResult;
        }

        private Guid? GetNodeContainerSpecificationIdFromName(string specificationName)
        {
            if (_nodeContainerSpecByName == null)
            {
                _nodeContainerSpecByName = _eventStore.Projections.Get<NodeContainerSpecificationsProjection>().Specifications.ToDictionary(s => s.Name.ToLower());
            }

            var key = specificationName.Trim().ToLower();

            if (_nodeContainerSpecByName.ContainsKey(key))
                return _nodeContainerSpecByName[key].Id;

            return null;
        }

        private List<NodeContainerForConversion> LoadDataFromConversionDatabase(string tableName)
        {
            List<NodeContainerForConversion> nodeContainersForConversions = new();

            using var dbConn = GetConnection();

            using var dbCmd = dbConn.CreateCommand();
            dbCmd.CommandText = "SELECT * FROM " + tableName + " WHERE status is null ORDER BY external_id";

            using var dbReader = dbCmd.ExecuteReader();

            while (dbReader.Read())
            {
                var routeNodeId = Guid.Parse(dbReader.GetString(1));
                var nodeContainerId = Guid.Parse(dbReader.GetString(2));
                var fromSpanEquipmentId = Guid.Parse(dbReader.GetString(3));
                var toSpanEquipmentId = Guid.Parse(dbReader.GetString(4));
                var fromStructureIndex = ushort.Parse(dbReader.GetString(5));
                var toStructureIndex = ushort.Parse(dbReader.GetString(6));
                var numberOfUnits = ushort.Parse(dbReader.GetString(7));

                //var conduit = new NodeContainerForConversion(externalId, specification, routeNodeId, nodeContainerId);

                //nodeContainersForConversions.Add(conduit);
            }

            dbConn.Close();

            return nodeContainersForConversions;
        }

        private class NodeContainerForConversion
        {
            public string ExternalId { get; set; }
            public string Specification { get; set; }
            public Guid NodeId { get; set; }
            public Guid NodeContainerId { get; set; }

            public NodeContainerForConversion(string externalId, string specification, Guid nodeId, Guid nodeContainerId)
            {
                ExternalId = externalId;
                Specification = specification;
                NodeId = nodeId;
                NodeContainerId = nodeContainerId;
            }
        }
    }
}
