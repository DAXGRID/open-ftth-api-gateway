using FluentResults;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenFTTH.APIGateway.Settings;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.TestData;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.Trace;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using OpenFTTH.UtilityGraphService.Business.Trace;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using OpenFTTH.RouteNetwork.Business.RouteElements.StateHandling;
using OpenFTTH.RouteNetwork.Business.RouteElements.Model;
using OpenFTTH.RouteNetwork.API.Queries;

namespace OpenFTTH.APIGateway.Conversion
{
    public class CablePlaceInConduit : ImporterBase
    {
        private static Guid _workTaskId = Guid.Parse("57859b8e-f033-4d3c-8226-88ae162a166e");

        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;
        private ILogger<ConduitSpanEquipmentImporter> _logger;
        private IEventStore _eventStore;
        private UtilityNetworkProjection _utilityNetwork;
        private LookupCollection<SpanEquipmentSpecification> _spanEquipmentSpecifications;
        private IRouteNetworkState _routeNetworkState;

        private string _tableName = "conversion.fibercables";

        private string _directInRouteTableName = "conversion.cable_in_route_segment_rels";

        public CablePlaceInConduit(ILogger<ConduitSpanEquipmentImporter> logger, IEventStore eventSTore, GeoDatabaseSetting geoDatabaseSettings, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IRouteNetworkState routeNetworkState) : base(geoDatabaseSettings)
        {
            _logger = logger;
            _eventStore = eventSTore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
            _spanEquipmentSpecifications = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;
            _routeNetworkState = routeNetworkState;
        }

        public void Run()
        {

            _logger.LogInformation("Starting span equipment cable conversion...");

            if (!CheckIfConversionTableExists(_tableName))
            {
                _logger.LogInformation($"{_tableName} don't exists. Will not import cables.");
                return;
            }


            CreateTableLogColumn(_tableName);

            var conduits = LoadSpanEquipmentsFromConversionDatabase(_tableName);


            AddCablesToNetwork(conduits);

            _logger.LogInformation("Conversion of span equipment cables finish!");
        }

        private List<SpanEquipmentForConversion> LoadSpanEquipmentsFromConversionDatabase(string tableName)
        {
            List<SpanEquipmentForConversion> conduitForConversions = new List<SpanEquipmentForConversion>();

            using var dbConn = GetConnection();

            using var dbCmd = dbConn.CreateCommand();
            dbCmd.CommandText = "SELECT external_id, span_equipment_id, span_equipment_spec_name, segment_ids, parent_span_equipment_ids, status FROM " + tableName + " WHERE external_id like 'CustomerTerminationFiberCable%' or external_id like 'TerminationDistributionPointFiberCable%'  ORDER BY external_id";

            using var dbReader = dbCmd.ExecuteReader();

            while (dbReader.Read())
            {
                var externalId = dbReader.GetString(0).Trim();
                var spanSegmentId = Guid.Parse(dbReader.GetString(1));
                var externalSpec = dbReader.GetString(2).Trim().ToLower();
                var routeSegmentsIds = dbReader.GetString(3);
                var parentConduits = dbReader.GetString(4);

                var conduit = new SpanEquipmentForConversion(spanSegmentId, externalId, externalSpec, routeSegmentsIds, parentConduits);

                conduitForConversions.Add(conduit);
            }

            dbConn.Close();

            return conduitForConversions;
        }

        private void AddCablesToNetwork(List<SpanEquipmentForConversion> spanEquipments)
        {
            using var conn = GetConnection();
                    
            using var logCmd = conn.CreateCommand() as NpgsqlCommand;

            foreach (var spanEquipment in spanEquipments)
            {

                var result = PlaceCableInConduit(logCmd, spanEquipment.Id, spanEquipment.ExternalId, spanEquipment.SpecificatonId, spanEquipment.SegmentIds, spanEquipment.ConduitRels);

                if (result.IsFailed)
                {
                    LogStatus((NpgsqlCommand)logCmd, _tableName, "GENEREL ROUTING FAILURE: " + result.Errors.First().Message, spanEquipment.ExternalId);
                }
                else
                {
                    LogStatus((NpgsqlCommand)logCmd, _tableName, "OK", spanEquipment.ExternalId);
                }
            }
        }

        public Result PlaceCableInConduit(NpgsqlCommand logCmd, Guid spanEquipmentId, string externalId, Guid specificationId, List<Guid> routeSegmentIds, List<CableConduitRel> conduitRels)
        {
            if (!_utilityNetwork.TryGetEquipment<SpanEquipment>(spanEquipmentId, out var cable))
            {
                var errorMsg = $"Connect find span equipment with id: {spanEquipmentId}";
                return Result.Fail(new Error(errorMsg));
            }

            var routeNetworkQueryResult = _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
               new GetRouteNetworkDetails(new InterestIdList() { cable.WalkOfInterestId })
               {
                   RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
               }
            ).Result;

            var cableWalkOfInterest = routeNetworkQueryResult.Value.Interests[cable.WalkOfInterestId];

            //var startNodeId = cableWalkOfInterest.RouteNetworkElementRefs.First();

            var endNodeId = cableWalkOfInterest.RouteNetworkElementRefs.Last();

            var routeNodeQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { endNodeId })
            {
                RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementOnly
            };

            var nodeQueryResult = _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNodeQuery).Result;


            List<SpanEquipment> conduitCandidates = new List<SpanEquipment>();

            foreach (var interest in nodeQueryResult.Value.RouteNetworkElements[endNodeId].InterestRelations)
            {
                if (interest.RefId != cableWalkOfInterest.Id && interest.RelationKind == RouteNetworkInterestRelationKindEnum.Start || interest.RelationKind == RouteNetworkInterestRelationKindEnum.End)
                {
                    // Get the conduit
                    if (_utilityNetwork.TryGetEquipment<SpanEquipment>(interest.RefId, out var conduitCandidate))
                    {
                        if (!conduitCandidate.IsCable)
                            conduitCandidates.Add(conduitCandidate);
                    }
                }
            }


            if (conduitCandidates.Count() == 1)
            {
                var conduitCandidate = conduitCandidates.First();

                var affixCommand = new AffixSpanEquipmentToParent(Guid.NewGuid(), new UserContext("conversion", _workTaskId), endNodeId, cable.SpanStructures[0].SpanSegments[0].Id, conduitCandidate.SpanStructures[0].SpanSegments[0].Id);
                var affixCommandResult = _commandDispatcher.HandleAsync<AffixSpanEquipmentToParent, Result>(affixCommand).Result;

                return affixCommandResult;
            }

            return Result.Ok();
        }



        private void LogCableToRouteNetworkRelations(string externalId, Guid routeSegmentId)
        {
            if (_geoDatabaseSetting.Database != null)
            {
                using var conn = GetConnection();

                using var cmd = conn.CreateCommand();

                cmd.CommandText = @"INSERT INTO " + _directInRouteTableName + "(external_id, route_segment_id) VALUES(";

                cmd.CommandText += "'" + externalId + "',";
                cmd.CommandText += "'" + routeSegmentId.ToString() + "');";
                cmd.ExecuteNonQuery();
            }
        }

      
   

        private void ExecuteStmt(IDbConnection conn, string sql)
        {
            Log.Debug(sql);
            var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 60 * 10; // 10 minutter
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
            cmd.Dispose();
        }

        private class SpanEquipmentForConversion
        {
            public Guid Id { get; set; }
            public string ExternalId { get; set; }
            public string ExternalSpec { get; set; }

            public List<Guid> SegmentIds = new List<Guid>();

            public List<CableConduitRel> ConduitRels = new List<CableConduitRel>();
            public Guid SpecificatonId { get; set; }

            public string AddressRemark = null;
            public bool MissingSegments = false;

            public SpanEquipmentForConversion(Guid id, string externalId, string externalSpec, string segmentIds, string parentConduits)
            {
                Id = id;
                ExternalId = externalId;
                ExternalSpec = externalSpec;

                //  segment id's
                var segmentSplit = segmentIds.Split(',');

                foreach (var segmentId in segmentSplit)
                {
                    if (segmentId.Length < 32)
                    {
                        MissingSegments = true;
                    }
                    else
                    {
                        SegmentIds.Add(Guid.Parse(segmentId));
                    }
                }

                // conduit rels
                if (!String.IsNullOrEmpty(parentConduits))
                {
                    var parentConduitsSplit = parentConduits.Split(',');

                    foreach (var parentConduit in parentConduitsSplit)
                    {
                        var conduitRelSplit = parentConduit.Split('#');

                        ConduitRels.Add(
                            new CableConduitRel()
                            {
                                SpanEquipmentId = Guid.Parse(conduitRelSplit[0]),
                                InnerConduitNumber = Int32.Parse(conduitRelSplit[1]),
                            }
                        );
                    }
                }


                SpecificatonId = MapToSpanEquipmentSpecification(externalSpec);
            }

            private Guid MapToSpanEquipmentSpecification(string externalSpec)
            {
                if (externalSpec.ToLower() == "24 fiber")
                    return TestSpecifications.FiberCable_24Fiber;
                else if (externalSpec.ToLower() == "12 fiber")
                    return TestSpecifications.FiberCable_12Fiber;
                else if (externalSpec.ToLower() == "4 fiber")
                    return TestSpecifications.FiberCable_4Fiber;
                else if (externalSpec.ToLower() == "2 fiber")
                    return TestSpecifications.FiberCable_2Fiber;
                else if (externalSpec.ToLower() == "72 fiber")
                    return TestSpecifications.FiberCable_72Fiber;
                else if (externalSpec.ToLower() == "48 fiber")
                    return TestSpecifications.FiberCable_48Fiber;
                else if (externalSpec.ToLower() == "96 fiber")
                    return TestSpecifications.FiberCable_96Fiber;


                throw new ApplicationException($"Cannot find spec name from external id: {externalSpec}");
            }

            public override string ToString()
            {
                string result = ExternalSpec + ": ";

                return result;
            }

        }

      
    }

  

}
