using OpenFTTH.Results;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenFTTH.APIGateway.Settings;
using OpenFTTH.CQRS;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.TestData;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.APIGateway.Conversion
{
    public class ConduitSpanEquipmentImporter : ImporterBase
    {
        private static Guid _neMultiConduitConversion = Guid.Parse("299c3e6f-c764-4566-81ab-3e9413aa4fca");

        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;
        private ILogger<ConduitSpanEquipmentImporter> _logger;
        private IEventStore _eventStore;


        //private string _tableName = "conversion.ne_multiconduit_conversion_result";
        private string _tableName = "conversion.conduit_conversion_result";

        public ConduitSpanEquipmentImporter(ILogger<ConduitSpanEquipmentImporter> logger, IEventStore eventSTore, GeoDatabaseSetting geoDatabaseSettings, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher) : base(geoDatabaseSettings)
        {
            _logger = logger;
            _eventStore = eventSTore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
        }

        public void Run()
        {

            _logger.LogInformation("Starting span equipment conversion...");

            CreateTableLogColumn(_tableName);

            var conduits = LoadSpanEquipmentsFromConversionDatabase(_tableName);


            AddConduitsToNetwork(conduits);

            _logger.LogInformation("Conversion of span equipment finish!");
        }

        private List<SpanEquipmentForConversion> LoadSpanEquipmentsFromConversionDatabase(string tableName)
        {
            List<SpanEquipmentForConversion> conduitForConversions = new List<SpanEquipmentForConversion>();

            using var dbConn = GetConnection();

            using var dbCmd = dbConn.CreateCommand();
            dbCmd.CommandText = "SELECT * FROM " + tableName + " WHERE status is null ORDER BY external_id";

            using var dbReader = dbCmd.ExecuteReader();

            while (dbReader.Read())
            {
                var externalId = dbReader.GetString(1).Trim();
                var externalSpec = dbReader.GetString(2).Trim().ToLower();
                var spanSegmentId = Guid.Parse(dbReader.GetString(3));
                var routeSegmentsIds = dbReader.GetString(4);
                Guid? accessAddressId = dbReader.IsDBNull(5) || dbReader.GetString(5).Trim() == "" ? null : Guid.Parse(dbReader.GetString(5));
                Guid? unitAddressId = dbReader.IsDBNull(6) || dbReader.GetString(6).Trim() == "" ? null : Guid.Parse(dbReader.GetString(6));
                var addressInfo = dbReader.IsDBNull(7) || dbReader.GetString(7).Trim() == "" ? null : dbReader.GetString(7).Trim();

                var conduit = new SpanEquipmentForConversion(spanSegmentId, externalId, externalSpec, routeSegmentsIds, accessAddressId, unitAddressId, addressInfo);

                conduitForConversions.Add(conduit);
            }

            dbConn.Close();

            return conduitForConversions;
        }

        private void AddConduitsToNetwork(List<SpanEquipmentForConversion> spanEquipments)
        {
            using var conn = GetConnection();

            using var logCmd = conn.CreateCommand() as NpgsqlCommand;

            foreach (var spanEquipment in spanEquipments)
            {
                if (spanEquipment.ConduitSpec != null)
                {
                    var result = PlaceConduitSpanEquipment(logCmd, spanEquipment.Id, spanEquipment.ExternalId, spanEquipment.ConduitSpec.SpecId, spanEquipment.SegmentIds, spanEquipment.ConduitSpec.AditionalSpecs, spanEquipment.ConduitSpec.MarkingColor, spanEquipment.AccessAddressId, spanEquipment.UnitAddressId, spanEquipment.AddressRemark);

                    if (result.IsFailed)
                    {
                        LogStatus((NpgsqlCommand)logCmd, _tableName, result.Errors.First().Message, spanEquipment.ExternalId);
                    }
                    else
                    {
                        LogStatus((NpgsqlCommand)logCmd, _tableName, "OK", spanEquipment.ExternalId);
                    }
                }
            }
        }

        private Result PlaceConduitSpanEquipment(NpgsqlCommand logCmd, Guid spanEquipmentId, string externalId, Guid specificationId, List<Guid> segmentIds, List<Guid> additionalStructureSpecIds, string markingColor, Guid? accessAddressId, Guid? unitAddressId, string? addressRemark)
        {
            Guid correlationId = Guid.NewGuid();

            RouteNetworkElementIdList walkIds = new RouteNetworkElementIdList();
            walkIds.AddRange(segmentIds);

            // Register walk of interest
            var walkOfInterestId = Guid.NewGuid();
            var registerWalkOfInterestCommand = new RegisterWalkOfInterest(correlationId, new UserContext("conversion", _neMultiConduitConversion), walkOfInterestId, walkIds);

            var registerWalkOfInterestCommandResult = _commandDispatcher.HandleAsync<RegisterWalkOfInterest, Result<RouteNetworkInterest>>(registerWalkOfInterestCommand).Result;

            if (registerWalkOfInterestCommandResult.IsFailed)
            {
                _logger.LogInformation("Failed to add conduit: " + externalId + " Error: " + registerWalkOfInterestCommandResult.Errors.First().Message);
                LogStatus((NpgsqlCommand)logCmd, _tableName, registerWalkOfInterestCommandResult.Errors.First().Message, externalId);
                return registerWalkOfInterestCommandResult;
            }

            // conduit name
            var nextConduitSeqStr = _eventStore.Sequences.GetNextVal("conduit").ToString();

            var conduitName = "R" + nextConduitSeqStr.PadLeft(6, '0');
            var namingInfo = new NamingInfo(conduitName, null);

            AddressInfo? addressInfo = null;

            if (accessAddressId != null || unitAddressId != null && addressRemark != null)
            {
                addressInfo = new AddressInfo() { AccessAddressId = accessAddressId, UnitAddressId = unitAddressId, Remark = addressRemark };
            }

            // Place conduit
            var placeSpanEquipmentCommand = new PlaceSpanEquipmentInRouteNetwork(correlationId, new UserContext("conversion", _neMultiConduitConversion), spanEquipmentId, specificationId, registerWalkOfInterestCommandResult.Value)
            {
                MarkingInfo = markingColor != null ? new MarkingInfo() { MarkingColor = markingColor } : null,
                NamingInfo = namingInfo,
                AddressInfo = addressInfo
            };

            var placeSpanEquipmentResult = _commandDispatcher.HandleAsync<PlaceSpanEquipmentInRouteNetwork, Result>(placeSpanEquipmentCommand).Result;

            if (placeSpanEquipmentResult.IsFailed)
            {
                _logger.LogInformation("Failed to add conduit: " + externalId + " Error: " + placeSpanEquipmentResult.Errors.First().Message);
                LogStatus((NpgsqlCommand)logCmd, _tableName, placeSpanEquipmentResult.Errors.First().Message, externalId);
                return placeSpanEquipmentResult;
            }

            // Place additional structures
            if (additionalStructureSpecIds.Count > 0)
            {
                var addStructure = new PlaceAdditionalStructuresInSpanEquipment(correlationId, new UserContext("conversion", _neMultiConduitConversion),
                 spanEquipmentId: placeSpanEquipmentCommand.SpanEquipmentId,
                 structureSpecificationIds: additionalStructureSpecIds.ToArray()
                );

                var addStructureResult = _commandDispatcher.HandleAsync<PlaceAdditionalStructuresInSpanEquipment, Result>(addStructure).Result;

                if (addStructureResult.IsFailed)
                {
                    _logger.LogInformation("Failed to add additional structures to: " + externalId + " Error: " + placeSpanEquipmentResult.Errors.First().Message);
                    LogStatus((NpgsqlCommand)logCmd, _tableName, addStructureResult.Errors.First().Message, externalId);
                    return addStructureResult;
                }
            }

            return Result.Ok();
        }

        private class SpanEquipmentForConversion
        {
            public Guid Id { get; set; }
            public string ExternalId { get; set; }
            public string ExternalSpec { get; set; }
            
            public List<Guid> SegmentIds = new List<Guid>();
            public SpanEquipmentSpecInfo ConduitSpec { get; set;}

            public Guid? AccessAddressId;
            public Guid? UnitAddressId;
            public string? AddressRemark;

            public bool MissingSegments = false;


            public SpanEquipmentForConversion(Guid id, string externalId, string externalSpec, string segmentIds, Guid? accessAddressId, Guid? unitAddressId, string? addressInfo)
            {
                Id = id;
                ExternalId = externalId;
                ExternalSpec = externalSpec;
                AccessAddressId = accessAddressId;
                UnitAddressId = unitAddressId;
                AddressRemark = addressInfo;

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

                ConduitSpec = MapToSpanEquipmentSpecification(externalSpec);
            }

            private SpanEquipmentSpecInfo MapToSpanEquipmentSpecification(string externalSpec)
            {
                // Ø 12
                if (externalSpec.StartsWith("12mm"))
                    return new SpanEquipmentSpecInfo(TestSpecifications.CustomerConduit_Ø12_Orange, GetMarkingText(externalSpec));

                if (externalSpec.StartsWith("ø12"))
                    return new SpanEquipmentSpecInfo(TestSpecifications.CustomerConduit_Ø12_Orange, GetMarkingText(externalSpec));

                // Ø 32
                if (externalSpec.StartsWith("ø32"))
                    return new SpanEquipmentSpecInfo(TestSpecifications.Multi_Ø32_3x10, GetMarkingText(externalSpec));

                // Ø40 flexrør
                if (externalSpec.StartsWith("flx"))
                    return new SpanEquipmentSpecInfo(TestSpecifications.Flex_Ø40_Red, GetAdditionalStructures(externalSpec));

                if (externalSpec.StartsWith("flexrør"))
                    return new SpanEquipmentSpecInfo(TestSpecifications.Flex_Ø40_Red, GetAdditionalStructures(externalSpec));

                if (externalSpec == "40-2x10-grøn-blå")
                    return new SpanEquipmentSpecInfo(TestSpecifications.Flex_Ø40_Red, GetAdditionalStructures(externalSpec));

                if (externalSpec == "40-3x10")
                    return new SpanEquipmentSpecInfo(TestSpecifications.Flex_Ø40_Red, GetMarkingText(externalSpec));

                // Ø40 tomrør
                if (externalSpec == "ø40 tomrør")
                    return new SpanEquipmentSpecInfo(TestSpecifications.Tomrør_Ø40_Orange, GetMarkingText(externalSpec));

                if (externalSpec == "ø40 dantex")
                    return new SpanEquipmentSpecInfo(TestSpecifications.Tomrør_Ø40_Orange, GetMarkingText(externalSpec));

                if (externalSpec == "ø40 4x10")
                    return new SpanEquipmentSpecInfo(TestSpecifications.Tomrør_Ø40_Orange, GetMarkingText(externalSpec));

                // Ø40 multi
                if (externalSpec == "ø40 5x10" || externalSpec == "ø40 5x10 dantex")
                    return new SpanEquipmentSpecInfo(TestSpecifications.Multi_Ø40_5x10, GetMarkingText(externalSpec));

                if (externalSpec == "ø40 5x10 rød")
                    return new SpanEquipmentSpecInfo(TestSpecifications.Multi_Ø40_5x10_Red, GetMarkingText(externalSpec));

                if (externalSpec.StartsWith("ø40+6x10"))
                    return new SpanEquipmentSpecInfo(TestSpecifications.Multi_Ø40_6x10, GetMarkingText(externalSpec));

                if (externalSpec.StartsWith("ø40 12x7"))
                    return new SpanEquipmentSpecInfo(TestSpecifications.Multi_Ø40_12x7, GetMarkingText(externalSpec));

                if (externalSpec.StartsWith("ø40+12x7"))
                    return new SpanEquipmentSpecInfo(TestSpecifications.Multi_Ø40_12x7, GetMarkingText(externalSpec));

                // Ø50 tomrør
                if (externalSpec == "ø50 dantex")
                    return new SpanEquipmentSpecInfo(TestSpecifications.Tomrør_Ø50_Orange);

                // Ø50
                if (externalSpec ==  "ø50 5x10")
                    return new SpanEquipmentSpecInfo(TestSpecifications.Multi_Ø40_5x10, GetMarkingText(externalSpec));

                if (externalSpec == "ø50 5x10+12x7 color")
                    return new SpanEquipmentSpecInfo(TestSpecifications.Multi_Ø50_12x7_5x10, GetMarkingText(externalSpec));

                if (externalSpec == "ø50+5x10+12x7dantex")
                    return new SpanEquipmentSpecInfo(TestSpecifications.Multi_Ø50_12x7_5x10, GetMarkingText(externalSpec));

                if (externalSpec == "ø50 5x10+12x7")
                    return new SpanEquipmentSpecInfo(TestSpecifications.Multi_Ø50_12x7_5x10_BlueYellow, GetMarkingText(externalSpec));

                if (externalSpec == "ø50 5x10+12x7 hvigrø")
                    return new SpanEquipmentSpecInfo(TestSpecifications.Multi_Ø50_12x7_5x10_GreenWhite, GetMarkingText(externalSpec));

                if (externalSpec.StartsWith("ø50+10x10") || externalSpec.StartsWith("ø50 10x10"))
                    return new SpanEquipmentSpecInfo(TestSpecifications.Multi_Ø50_10x10, GetMarkingText(externalSpec));


                Log.Warning($"Don't know how to handle spec: '{externalSpec}'");

                return null;
            }

            private string GetMarkingText(string spec)
            {
                if (spec.Contains("core"))
                    return "Sort streg";

                return null;
            }

            private List<Guid> GetAdditionalStructures(string spec)
            {
                List<Guid> additionalStructureSpecs = new List<Guid>();

                if (spec.Contains("1x10 sort"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Black);

                if (spec.Contains("1x10 blå"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Blue);

                if (spec.Contains("1x10 grå"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Silver);

                if (spec.Contains("1x10 hvid"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_White);

                if (spec.Contains("1x10 gul"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Yellow);

                if (spec.Contains("1x10 brun"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Brown);

                if (spec.Contains("1x10 grøn"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Green);

                if (spec.Contains("1x10 orange"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Orange);

                if (spec.Contains("1x10 rød"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Red);

                if (spec.Contains("1x10 pink"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Pink);

                if (spec.Contains("1x10 turkis"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Turquoise);

                if (spec.Contains("1x10 violet"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Violet);

                if (spec.Contains("blågul"))
                {
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Blue);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Yellow);
                }

                return additionalStructureSpecs;
            }

            public override string ToString()
            {
                string result = ExternalSpec + ": ";
                if (ConduitSpec != null)
                {
                    result += "MARKING: " + ConduitSpec.MarkingColor + " ADDS: " + ConduitSpec.AditionalSpecs.Count;

                }
                else
                {
                    result += " NO SPEC MAPPING";
                }

                return result;
            }

        }

        private class SpanEquipmentSpecInfo
        {
            public Guid SpecId { get; set; }
            public string MarkingColor { get; set; }

            public List<Guid> AditionalSpecs = new List<Guid>();

            public SpanEquipmentSpecInfo(Guid specId, string markingColor = null)
            {
                SpecId = specId;
                MarkingColor = markingColor;
            }

            public SpanEquipmentSpecInfo(Guid specId, List<Guid> aditionalSpecs)
            {
                SpecId = specId;
                AditionalSpecs = aditionalSpecs;
            }
        }
    }
}
