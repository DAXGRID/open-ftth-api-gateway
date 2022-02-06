﻿using FluentResults;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenFTTH.APIGateway.Settings;
using OpenFTTH.CQRS;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.NodeContainers.Projections;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Projections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.APIGateway.Conversion
{
    public class TerminalEquipmentImporter : ImporterBase
    {
        private ILogger<TerminalEquipmentImporter> _logger;
        private Guid _workTaskId;
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;
        private UtilityNetworkProjection _utilityNetwork;

        private string _terminalEquipmentTableName = "conversion.terminal_equipment";
        

        private Dictionary<string, TerminalEquipmentSpecification> _terminalEquipmentSpecByName = null;

        public TerminalEquipmentImporter(ILogger<TerminalEquipmentImporter> logger, Guid workTaskId, IEventStore eventSTore, GeoDatabaseSetting geoDatabaseSettings, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher) : base(geoDatabaseSettings)
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
            _logger.LogInformation("Conversion of terminal equipment(s) started...");

            if (!CheckIfConversionTableExists(_terminalEquipmentTableName))
            {
                _logger.LogInformation($"{_terminalEquipmentTableName} don't exists. Will not import terminal equipments.");
                return;
            }


            CreateTableLogColumn(_terminalEquipmentTableName);

            var terminalEquipments = LoadDataFromConversionDatabase();

            AddTerminalEquipmentsToNetwork(terminalEquipments.Values.ToList());

            _logger.LogInformation("Conversion of erminal equipment(s) finish!");
        }

        private void AddTerminalEquipmentsToNetwork(List<TerminalEquipmentForConversion> terminalEquipments)
        {
            using var conn = GetConnection();

            using var logCmd = conn.CreateCommand() as NpgsqlCommand;

            foreach (var terminalEquipment in terminalEquipments)
            {
                // Place the rack
                var specId = GetTerminalEquipmentSpecificationIdFromName(terminalEquipment.Specification);

                if (specId != null)
                {
                    var relatedInfo = GetRelatedInformation(terminalEquipment.NodeId);

                    var placeNodeContainerResult = PlaceTerminalEquipment(logCmd, terminalEquipment, specId.Value, relatedInfo);

                    if (placeNodeContainerResult.IsSuccess)
                    {
                        LogStatus((NpgsqlCommand)logCmd, _terminalEquipmentTableName, "OK", terminalEquipment.ExternalId);
                    }
                    else
                    {
                        LogStatus((NpgsqlCommand)logCmd, _terminalEquipmentTableName, placeNodeContainerResult.Errors.First().Message, terminalEquipment.ExternalId);
                    }
                }
                else
                {
                    LogStatus((NpgsqlCommand)logCmd, _terminalEquipmentTableName, $"Cannot find any rack specification with name: '{terminalEquipment.Specification}'", terminalEquipment.ExternalId);
                }
            }
        }
     
        private Result PlaceTerminalEquipment(NpgsqlCommand logCmd, TerminalEquipmentForConversion terminalEquipment, Guid specId, RelatedEquipmentInfo relatedInfo)
        {
            Guid correlationId = Guid.NewGuid();

            var commandUserContext = new UserContext("conversion", _workTaskId)
            {
                EditingRouteNodeId = terminalEquipment.NodeId
            };

            // Check if node container already exists
            if (relatedInfo.NodeContainer == null)
            {
                if (_utilityNetwork.TryGetEquipment<NodeContainer>(relatedInfo.NodeContainer.Id, out var existingNodeContainer))
                {
                    System.Diagnostics.Debug.WriteLine($"No container exists in node: {terminalEquipment.NodeId}");
                    return Result.Fail(new Error($"No container exists in node: {terminalEquipment.NodeId}"));
                }
            }
            

            var terminalEquipmentSpecificationId = GetTerminalEquipmentSpecificationIdFromName(terminalEquipment.Specification);

            if (terminalEquipmentSpecificationId == null)
                return Result.Fail(new Error($"Cannot find terminal equipment specfication: {terminalEquipment.Specification}"));


            // Stand-alone splice equipment in node
            if (terminalEquipment.RackId == Guid.Empty)
            {
                var placeEqCmd = new PlaceTerminalEquipmentInNodeContainer(
                    correlationId: Guid.NewGuid(),
                    userContext: commandUserContext,
                    nodeContainerId: relatedInfo.NodeContainer.Id,
                    terminalEquipmentId: terminalEquipment.TerminalEquipmentId,
                    terminalEquipmentSpecificationId: terminalEquipmentSpecificationId.Value,
                    numberOfEquipments: 1,
                    startSequenceNumber: 1,
                    namingMethod: TerminalEquipmentNamingMethodEnum.NameAndNumber,
                    namingInfo: new NamingInfo("Splidsemuffe", null)
                );

                var placeEqResult = _commandDispatcher.HandleAsync<PlaceTerminalEquipmentInNodeContainer, Result>(placeEqCmd).Result;

                LogStatus((NpgsqlCommand)logCmd, _terminalEquipmentTableName, "external_id", terminalEquipment.ExternalId, placeEqResult);

                return placeEqResult;
            }
            else
            {
                // Rack equipment
                var placeEqCmd = new PlaceTerminalEquipmentInNodeContainer(
                    correlationId: Guid.NewGuid(),
                    userContext: commandUserContext,
                    nodeContainerId: relatedInfo.NodeContainer.Id,
                    terminalEquipmentId: terminalEquipment.TerminalEquipmentId,
                    terminalEquipmentSpecificationId: terminalEquipmentSpecificationId.Value,
                    numberOfEquipments: 1,
                    startSequenceNumber: 80 - terminalEquipment.RackPosition,
                    namingMethod: TerminalEquipmentNamingMethodEnum.NumberOnly,
                    namingInfo: null
                )
                {
                    SubrackPlacementInfo = new SubrackPlacementInfo(terminalEquipment.RackId, terminalEquipment.RackPosition, SubrackPlacmentMethod.BottomUp)
                };

                var placeEqResult = _commandDispatcher.HandleAsync<PlaceTerminalEquipmentInNodeContainer, Result>(placeEqCmd).Result;

                LogStatus((NpgsqlCommand)logCmd, _terminalEquipmentTableName, "external_id", terminalEquipment.ExternalId, placeEqResult);

                return placeEqResult;

            }
        }

        private Guid? GetTerminalEquipmentSpecificationIdFromName(string specificationName)
        {
            if (_terminalEquipmentSpecByName == null)
            {
                _terminalEquipmentSpecByName = new Dictionary<string, TerminalEquipmentSpecification>();

                foreach (var teSpec in _eventStore.Projections.Get<TerminalEquipmentSpecificationsProjection>().Specifications)
                {
                    if (!_terminalEquipmentSpecByName.ContainsKey(teSpec.Name.ToLower()))
                        _terminalEquipmentSpecByName[teSpec.Name.ToLower()] = teSpec;
                }
            }

            var key = specificationName.Trim().ToLower();

            if (_terminalEquipmentSpecByName.ContainsKey(key))
                return _terminalEquipmentSpecByName[key].Id;

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

        private Dictionary<Guid, TerminalEquipmentForConversion> LoadDataFromConversionDatabase()
        {
            var nodeContainersForConversions = LoadTerminalEquipmentsToBeImportedFromDatabase();

            return nodeContainersForConversions;
        }

        private Dictionary<Guid, TerminalEquipmentForConversion> LoadTerminalEquipmentsToBeImportedFromDatabase()
        {
            Dictionary<Guid, TerminalEquipmentForConversion> termianEquipmentsForConversions = new();

            using var dbConn = GetConnection();

            // Load terminal equipments
            using var terminalEquipmentSelectCmd = dbConn.CreateCommand();
            terminalEquipmentSelectCmd.CommandText = "SELECT external_id, external_spec, route_node_id, terminal_equipment_id, specification, name, rack_id, rack_position FROM " + _terminalEquipmentTableName + " WHERE status is null ORDER BY external_id";

            using var terminalEquipmentReader = terminalEquipmentSelectCmd.ExecuteReader();

            while (terminalEquipmentReader.Read())
            {
                var terminalEquipment = new TerminalEquipmentForConversion();

                var externalSpec = terminalEquipmentReader.GetString(1).Trim();

                terminalEquipment.ExternalId = terminalEquipmentReader.GetString(0).Trim();
                terminalEquipment.NodeId = Guid.Parse(terminalEquipmentReader.GetString(2));
                terminalEquipment.TerminalEquipmentId = Guid.Parse(terminalEquipmentReader.GetString(3));
                terminalEquipment.Specification = terminalEquipmentReader.GetString(4).Trim();
                terminalEquipment.Name = terminalEquipmentReader.GetString(5).Trim();
                terminalEquipment.RackId = Guid.Parse(terminalEquipmentReader.GetString(6));
                terminalEquipment.RackPosition = Int32.Parse(terminalEquipmentReader.GetString(7).Trim());

                termianEquipmentsForConversions.Add(terminalEquipment.TerminalEquipmentId, terminalEquipment);
            }

            dbConn.Close();

            return termianEquipmentsForConversions;
        }

        private class TerminalEquipmentForConversion
        {
            public string ExternalId { get; set; }
            public string Specification { get; set; }
            public Guid NodeId { get; set; }
            public Guid TerminalEquipmentId { get; set; }
            public string Name { get; set; }
            public Guid RackId { get; set; }
            public int RackPosition { get; set; }
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

