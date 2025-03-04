using OpenFTTH.Results;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenFTTH.APIGateway.Settings;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.RouteNetwork.API.Commands;
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
    public class NodeContainerImporter : ImporterBase
    {
        private ILogger<NodeContainerImporter> _logger;
        private Guid _workTaskId;
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;
        private UtilityNetworkProjection _utilityNetwork;

        private string _nodeContainerTableName = "conversion.node_containers";
        private string _connectivityTableName = "conversion.connectivity";


        private Dictionary<string, NodeContainerSpecification> _nodeContainerSpecByName = null;

        public NodeContainerImporter(ILogger<NodeContainerImporter> logger, Guid workTaskId, IEventStore eventSTore, GeoDatabaseSetting geoDatabaseSettings, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher) : base(geoDatabaseSettings)
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
            _logger.LogInformation("Conversion of node containers started...");

            if (!CheckIfConversionTableExists(_nodeContainerTableName))
            {
                _logger.LogInformation($"{_nodeContainerTableName} don't exists. Will not import node containers.");
                return;
            }

            CreateTableLogColumn(_nodeContainerTableName);
            //CreateTableLogColumn(_connectivityTableName);

            var nodeContainers = LoadDataFromConversionDatabase();

            AddContainersToNetwork(nodeContainers.Values.ToList());

            _logger.LogInformation("Conversion of node containers finish!");
        }

        private void AddContainersToNetwork(List<NodeContainerForConversion> nodeContainers)
        {
            using var conn = GetConnection();

            using var logCmd = conn.CreateCommand() as NpgsqlCommand;

            foreach (var nodeContainer in nodeContainers)
            {
                // Place the container
                var specId = GetNodeContainerSpecificationIdFromName(nodeContainer.Specification);

                if (specId != null)
                {
                    var relatedInfo = GetRelatedInformation(nodeContainer.NodeId);

                    var placeNodeContainerResult = PlaceNodeContainer(logCmd, nodeContainer, specId.Value, relatedInfo);

                    if (placeNodeContainerResult.IsSuccess)
                    {
                        LogStatus((NpgsqlCommand)logCmd, _nodeContainerTableName, "OK", nodeContainer.ExternalId);
                        //AffixSpanEquipmentsToNodeContainer(logCmd, nodeContainer, relatedInfo);
                        //ConnectSpanEquipmentsInNodeContainer(logCmd, nodeContainer, relatedInfo);
                    }
                    else
                    {
                        LogStatus((NpgsqlCommand)logCmd, _nodeContainerTableName, placeNodeContainerResult.Errors.First().Message, nodeContainer.ExternalId);
                    }
                }
                else
                {
                    LogStatus((NpgsqlCommand)logCmd, _nodeContainerTableName, $"Cannot find any node container specification with name: '{nodeContainer.Specification}'", nodeContainer.ExternalId);
                }
            }
        }

        private void AffixSpanEquipmentsToNodeContainer(NpgsqlCommand logCmd, NodeContainerForConversion nodeContainer, RelatedEquipmentInfo relatedInfo)
        {
            if (!relatedInfo.SingleConduitsOnly)
            {
                foreach (var ingoingSpanEquipment in relatedInfo.IngoingSpanEquipments)
                {
                    if (ingoingSpanEquipment.SpanStructures.Length == 1)
                        AffixSpanEquipmentToContainer(nodeContainer.NodeId, ingoingSpanEquipment.Id, nodeContainer.NodeContainerId, NodeContainerSideEnum.North);
                    else
                        AffixSpanEquipmentToContainer(nodeContainer.NodeId, ingoingSpanEquipment.Id, nodeContainer.NodeContainerId, NodeContainerSideEnum.West);
                }

                foreach (var outgoingSpanEquipment in relatedInfo.OutgoingSpanEquipments)
                {
                    if (outgoingSpanEquipment.SpanStructures.Length == 1)
                        AffixSpanEquipmentToContainer(nodeContainer.NodeId, outgoingSpanEquipment.Id, nodeContainer.NodeContainerId, NodeContainerSideEnum.North);
                    else
                        AffixSpanEquipmentToContainer(nodeContainer.NodeId, outgoingSpanEquipment.Id, nodeContainer.NodeContainerId, NodeContainerSideEnum.East);
                }
            }
            else
            {
                foreach (var ingoingSpanEquipment in relatedInfo.IngoingSpanEquipments)
                {
                    AffixSpanEquipmentToContainer(nodeContainer.NodeId, ingoingSpanEquipment.Id, nodeContainer.NodeContainerId, NodeContainerSideEnum.West);
                }

                foreach (var outgoingSpanEquipment in relatedInfo.OutgoingSpanEquipments)
                {
                    AffixSpanEquipmentToContainer(nodeContainer.NodeId, outgoingSpanEquipment.Id, nodeContainer.NodeContainerId, NodeContainerSideEnum.East);
                }
            }
        }

        private void ConnectSpanEquipmentsInNodeContainer(NpgsqlCommand logCmd, NodeContainerForConversion nodeContainer, RelatedEquipmentInfo relatedInfo)
        {
            foreach (var connectivity in nodeContainer.Connectivity)
            {
                if (!relatedInfo.SpanEquipmentById.ContainsKey(connectivity.FromSpanEquipmentId))
                {
                    LogStatus(logCmd, _connectivityTableName, "ogc_fid", connectivity.Key, $"Span equipment with id: {connectivity.FromSpanEquipmentId} not found in route node with id: {nodeContainer.NodeId}");
                    continue;
                }

                if (!relatedInfo.SpanEquipmentById.ContainsKey(connectivity.ToSpanEquipmentId))
                {
                    LogStatus(logCmd, _connectivityTableName, "ogc_fid", connectivity.Key, $"Span equipment with id: {connectivity.ToSpanEquipmentId} not found in route node with id: {nodeContainer.NodeId}");
                    continue;
                }

                // We need adjust index 1 to index 0 on single conduits
                var fromSpanEquipment = relatedInfo.SpanEquipmentById[connectivity.FromSpanEquipmentId];
                var toSpanEquipment = relatedInfo.SpanEquipmentById[connectivity.ToSpanEquipmentId];

                int fromStructureIndex = fromSpanEquipment.SpanStructures.Length == 1 ? connectivity.FromStructureIndex - 1 : connectivity.FromStructureIndex;
                int toStructureIndex = toSpanEquipment.SpanStructures.Length == 1 ? connectivity.ToStructureIndex - 1 : connectivity.ToStructureIndex;

                var connectResult = ConnectSpanEquipments(nodeContainer.NodeId, connectivity.FromSpanEquipmentId, connectivity.ToSpanEquipmentId, (ushort)fromStructureIndex, (ushort)toStructureIndex, connectivity.NumberOfUnits);

                if (connectResult.IsFailed)
                {
                    LogStatus((NpgsqlCommand)logCmd, _connectivityTableName, "ogc_fid", connectivity.Key, connectResult.Errors.First().Message);
                }

                LogStatus(logCmd, _connectivityTableName, "ogc_fid", connectivity.Key, connectResult);
            }
        }

        private Result PlaceNodeContainer(NpgsqlCommand logCmd, NodeContainerForConversion nodeContainer, Guid specId, RelatedEquipmentInfo relatedInfo)
        {
            Guid correlationId = Guid.NewGuid();

            var commandUserContext = new UserContext("conversion", _workTaskId)
            {
                EditingRouteNodeId = nodeContainer.NodeId
            };

            // Check if node container already exists
            if (relatedInfo.NodeContainer != null)
            {
                if (_utilityNetwork.TryGetEquipment<NodeContainer>(relatedInfo.NodeContainer.Id, out var existingNodeContainer))
                {
                    System.Diagnostics.Debug.WriteLine($"Node container alreadu exists in node: {nodeContainer.NodeId}");
                    return Result.Ok();
                }
            }

            // First register the node of interest where to place the node container
            var nodeOfInterestId = Guid.NewGuid();

            var registerNodeOfInterestCommand = new RegisterNodeOfInterest(correlationId, commandUserContext, nodeOfInterestId, nodeContainer.NodeId);

            var registerNodeOfInterestCommandResult = _commandDispatcher.HandleAsync<RegisterNodeOfInterest, Result<RouteNetworkInterest>>(registerNodeOfInterestCommand).Result;

            if (registerNodeOfInterestCommandResult.IsFailed)
            {
                LogStatus((NpgsqlCommand)logCmd, _nodeContainerTableName, registerNodeOfInterestCommandResult.Errors.First().Message, nodeContainer.ExternalId);
                return registerNodeOfInterestCommandResult;
            }

            // Now place the node container in the walk
            var placeNodeContainerCommand = new PlaceNodeContainerInRouteNetwork(correlationId, commandUserContext, nodeContainer.NodeContainerId, specId, registerNodeOfInterestCommandResult.Value)
            {
                ManufacturerId = null,
                LifecycleInfo = new LifecycleInfo(DeploymentStateEnum.InService, null, null)
            };

            var placeNodeContainerResult = _commandDispatcher.HandleAsync<PlaceNodeContainerInRouteNetwork, Result>(placeNodeContainerCommand).Result;

            // Unregister interest if place node container failed
            if (placeNodeContainerResult.IsFailed)
            {
                LogStatus((NpgsqlCommand)logCmd, _nodeContainerTableName, placeNodeContainerResult.Errors.First().Message, nodeContainer.ExternalId);

                var unregisterCommandResult = _commandDispatcher.HandleAsync<UnregisterInterest, Result>(new UnregisterInterest(correlationId, commandUserContext, nodeOfInterestId)).Result;

                if (unregisterCommandResult.IsFailed)
                    return unregisterCommandResult;
            }

            LogStatus((NpgsqlCommand)logCmd, _nodeContainerTableName, "external_id", nodeContainer.ExternalId, placeNodeContainerResult);

            return placeNodeContainerResult;
        }

        private Result AffixSpanEquipmentToContainer(Guid routeNodeId, Guid spanEquipmentId, Guid nodeContainerId, NodeContainerSideEnum side)
        {
            Guid correlationId = Guid.NewGuid();

            var commandUserContext = new UserContext("conversion", _workTaskId)
            {
                EditingRouteNodeId = routeNodeId
            };

            var affixConduitToContainerCommand = new AffixSpanEquipmentToNodeContainer(correlationId, commandUserContext,
               spanEquipmentOrSegmentId: spanEquipmentId,
               nodeContainerId: nodeContainerId,
               nodeContainerIngoingSide: side
           );

            var affixResult = _commandDispatcher.HandleAsync<AffixSpanEquipmentToNodeContainer, Result>(affixConduitToContainerCommand).Result;

            return affixResult;
        }

        private Result ConnectSpanEquipments(Guid routeNodeId, Guid fromSpanEquipmentId, Guid toSpanEquipmentId, ushort fromStructureIndex, ushort toStructureIndex, int numberOfUnits)
        {
            Guid correlationId = Guid.NewGuid();

            var commandUserContext = new UserContext("conversion", _workTaskId)
            {
                EditingRouteNodeId = routeNodeId
            };

            var connectCmd = new ConnectSpanSegmentsByIndexAtRouteNode(correlationId, commandUserContext,
                routeNodeId: routeNodeId,
                fromSpanEquipmentId: fromSpanEquipmentId,
                toSpanEquipmentId: toSpanEquipmentId,
                fromStructureIndex: fromStructureIndex,
                toStructureIndex: toStructureIndex,
                numberOfUnits: numberOfUnits
            );

            var connectResult = _commandDispatcher.HandleAsync<ConnectSpanSegmentsByIndexAtRouteNode, Result>(connectCmd).Result;

            return connectResult;
        }


        private Guid? GetNodeContainerSpecificationIdFromName(string specificationName)
        {
            if (_nodeContainerSpecByName == null)
            {
                _nodeContainerSpecByName = new Dictionary<string, NodeContainerSpecification>();

                foreach (var nodeSpec in _eventStore.Projections.Get<NodeContainerSpecificationsProjection>().Specifications)
                {
                    if (!_nodeContainerSpecByName.ContainsKey(nodeSpec.Name.ToLower()))
                        _nodeContainerSpecByName[nodeSpec.Name.ToLower()] = nodeSpec;
                }
            }

            var key = specificationName.Trim().ToLower();

            if (_nodeContainerSpecByName.ContainsKey(key))
                return _nodeContainerSpecByName[key].Id;

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

        private Dictionary<Guid, NodeContainerForConversion> LoadDataFromConversionDatabase()
        {
            var nodeContainersForConversions = LoadNodeContainersFromDatabase();

            var nodeContainersWitConnectivity = LoadConnectivityFromConversionDatabase(nodeContainersForConversions);

            return nodeContainersWitConnectivity;
        }

        private Dictionary<Guid, NodeContainerForConversion> LoadNodeContainersFromDatabase()
        {
            Dictionary<Guid, NodeContainerForConversion> nodeContainersForConversions = new();

            using var dbConn = GetConnection();

            // Load node containers
            using var nodeContainerSelectCmd = dbConn.CreateCommand();
            nodeContainerSelectCmd.CommandText = "SELECT external_id, external_spec, route_node_id, node_container_id, specification FROM " + _nodeContainerTableName + " WHERE status is null ORDER BY external_id";

            using var nodeContainerReader = nodeContainerSelectCmd.ExecuteReader();

            while (nodeContainerReader.Read())
            {
                var externalId = nodeContainerReader.GetString(0).Trim();
                var externalSpec = nodeContainerReader.GetString(1).Trim();
                var routeNodeId = Guid.Parse(nodeContainerReader.GetString(2));
                var nodeContainerId = Guid.Parse(nodeContainerReader.GetString(3));
                var specification = nodeContainerReader.GetString(4).Trim();

                var nodeCondtainerForConversion = new NodeContainerForConversion(externalId, specification, routeNodeId, nodeContainerId);

                nodeContainersForConversions.Add(nodeContainerId, nodeCondtainerForConversion);
            }

            dbConn.Close();

            return nodeContainersForConversions;
        }

        private Dictionary<Guid, NodeContainerForConversion> LoadConnectivityFromConversionDatabase(Dictionary<Guid, NodeContainerForConversion> nodeContainersForConversions)
        {
            /*
            using var dbConn = GetConnection();
         
            // Load connectivity
            var connectivitySelectCmd = dbConn.CreateCommand();
            connectivitySelectCmd.CommandText = "SELECT * FROM " + _connectivityTableName + " WHERE status is null ORDER BY node_container_id";

            using var connectivityReader = connectivitySelectCmd.ExecuteReader();

            while (connectivityReader.Read())
            {
                var connectivity = new ConnectivityForConversion();

                connectivity.Key = connectivityReader.GetInt32(0).ToString();
                connectivity.RouteNodeId = Guid.Parse(connectivityReader.GetString(1));
                connectivity.NodeContainerId = Guid.Parse(connectivityReader.GetString(2));
                connectivity.FromSpanEquipmentId = Guid.Parse(connectivityReader.GetString(3));
                connectivity.ToSpanEquipmentId = Guid.Parse(connectivityReader.GetString(4));
                connectivity.FromStructureIndex = (ushort)connectivityReader.GetInt32(5);
                connectivity.ToStructureIndex = (ushort)connectivityReader.GetInt32(6);
                connectivity.NumberOfUnits = connectivityReader.GetInt32(7);

                if (nodeContainersForConversions.ContainsKey(connectivity.NodeContainerId))
                {
                    nodeContainersForConversions[connectivity.NodeContainerId].Connectivity.Add(connectivity);
                }
            }

            dbConn.Close();

            */
            return nodeContainersForConversions;

        }

        private class NodeContainerForConversion
        {
            public string ExternalId { get; set; }
            public string Specification { get; set; }
            public Guid NodeId { get; set; }
            public Guid NodeContainerId { get; set; }

            public List<ConnectivityForConversion> Connectivity = new List<ConnectivityForConversion>();

            public NodeContainerForConversion(string externalId, string specification, Guid nodeId, Guid nodeContainerId)
            {
                ExternalId = externalId;
                Specification = specification;
                NodeId = nodeId;
                NodeContainerId = nodeContainerId;
            }
        }

        private class ConnectivityForConversion
        {
            public string Key { get; set; }
            public Guid RouteNodeId { get; set; }
            public Guid NodeContainerId { get; set; }
            public Guid FromSpanEquipmentId { get; set; }
            public Guid ToSpanEquipmentId { get; set; }
            public ushort FromStructureIndex { get; set; }
            public ushort ToStructureIndex { get; set; }
            public int NumberOfUnits { get; set; }
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

