using FluentResults;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.TestData;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace OpenFTTH.APIGateway.TestData
{
    public class ConduitSeeder
    {
        private static string _connectionString = Environment.GetEnvironmentVariable("conversion_database_connection_string");

        private static Guid _bentleyMultiConduitConversion = Guid.Parse("9501d6d6-2322-44a5-bdfe-c55c371d02e4");

        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;
        private ILogger<ConduitSeeder> _logger;
        private ILoggerFactory _loggerFactory;
        private IEventStore _eventStore;


        public ConduitSeeder(ILoggerFactory loggerFactory, ILogger<ConduitSeeder> logger, IEventStore eventSTore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _loggerFactory = loggerFactory;
            _logger = logger;
            _eventStore = eventSTore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
        }

        public void Run()
        {
            _logger.LogInformation("Starting conduit seed...");

            if (_connectionString != null)
            {
                var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

                if (utilityNetwork.SpanEquipmentsByEquipmentId.Count > 0)
                {
                    _logger.LogInformation("Database already contain converted data. Will therefor not seed conversion data.");
                    return;
                }

                _logger.LogInformation("Creating specifications...");
                new TestSpecifications(_loggerFactory, _commandDispatcher, _queryDispatcher).Run();

                _logger.LogInformation("Adding span equipments...");
                var conduits = LoadConduitsFromConversionDatabase();
                AddConduitsToNetwork(conduits);

                _logger.LogInformation("Seeding of conduits finish!");
            }
            else
            {
                _logger.LogInformation("No conversion_database_connection_string env variable specified. Will not seed database with conversion data");
            }


        }

        private List<ConduitForConversion> LoadConduitsFromConversionDatabase()
        {
            List<ConduitForConversion> conduitForConversions = new List<ConduitForConversion>();

            using var dbConn = GetConnection();
            dbConn.Open();

            using var dbCmd = dbConn.CreateCommand();
            dbCmd.CommandText = "SELECT * FROM conversion.bentley_multiconduit_conversion_result";

            using var dbReader = dbCmd.ExecuteReader();

            while (dbReader.Read())
            {
                var external_id = dbReader.GetString(1).Trim();
                var external_spec = dbReader.GetString(2).Trim().ToLower();
                var segments_ids = dbReader.GetString(3);

                var conduit = new ConduitForConversion(external_id, external_spec, segments_ids);

                conduitForConversions.Add(conduit);
            }

            dbConn.Close();

            return conduitForConversions;
        }

        private void AddConduitsToNetwork(List<ConduitForConversion> conduits)
        {
            foreach (var conduit in conduits)
            {
                if (conduit.ConduitSpec != null)
                {
                    PlaceConduit(conduit.ExternalId, conduit.ConduitSpec.SpecId, conduit.SegmentIds, conduit.ConduitSpec.AditionalSpecs, conduit.ConduitSpec.MarkingColor);
                }
            }
        }

        private void PlaceConduit(string externalId, Guid specificationId, List<Guid> segmentIds, List<Guid> additionalStructureSpecIds, string markingColor)
        {
            Guid correlationId = Guid.NewGuid();

            RouteNetworkElementIdList walkIds = new RouteNetworkElementIdList();
            walkIds.AddRange(segmentIds);

            // Register walk of interest
            var walkOfInterestId = Guid.NewGuid();
            var registerWalkOfInterestCommand = new RegisterWalkOfInterest(correlationId, new UserContext("conversion", _bentleyMultiConduitConversion), walkOfInterestId, walkIds);

            var registerWalkOfInterestCommandResult = _commandDispatcher.HandleAsync<RegisterWalkOfInterest, Result<RouteNetworkInterest>>(registerWalkOfInterestCommand).Result;

            if (registerWalkOfInterestCommandResult.IsFailed)
            {
                _logger.LogInformation("Failed to add conduit: " + externalId + " Error: " + registerWalkOfInterestCommandResult.Errors.First().Message);
                return;
            }

            // conduit name
            var nextConduitSeqStr = _eventStore.Sequences.GetNextVal("conduit").ToString();

            var conduitName = "R" + nextConduitSeqStr.PadLeft(6, '0');
            var namingInfo = new NamingInfo(conduitName, null);

            // Place conduit
            var placeSpanEquipmentCommand = new PlaceSpanEquipmentInRouteNetwork(correlationId, new UserContext("conversion", _bentleyMultiConduitConversion), Guid.NewGuid(), specificationId, registerWalkOfInterestCommandResult.Value)
            {
                MarkingInfo = markingColor != null ? new MarkingInfo() { MarkingColor = markingColor } : null,
                NamingInfo = namingInfo
            };

            var placeSpanEquipmentResult = _commandDispatcher.HandleAsync<PlaceSpanEquipmentInRouteNetwork, Result>(placeSpanEquipmentCommand).Result;

            if (placeSpanEquipmentResult.IsFailed)
            {
                _logger.LogInformation("Failed to add conduit: " + externalId + " Error: " + placeSpanEquipmentResult.Errors.First().Message);
                return;
            }

            // Place additional structures
            if (additionalStructureSpecIds.Count > 0)
            {
                var addStructure = new PlaceAdditionalStructuresInSpanEquipment(correlationId, new UserContext("conversion", _bentleyMultiConduitConversion),
                 spanEquipmentId: placeSpanEquipmentCommand.SpanEquipmentId,
                 structureSpecificationIds: additionalStructureSpecIds.ToArray()
                );

                var addStructureResult = _commandDispatcher.HandleAsync<PlaceAdditionalStructuresInSpanEquipment, Result>(addStructure).Result;

                if (addStructureResult.IsFailed)
                {
                    _logger.LogInformation("Failed to add additional structures to: " + externalId + " Error: " + placeSpanEquipmentResult.Errors.First().Message);
                    return;
                }
            }
        }

        private IDbConnection GetConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }

        private class ConduitForConversion
        {
            public string ExternalId { get; set; }
            public string ExternalSpec { get; set; }
            public List<Guid> SegmentIds = new List<Guid>();

            public ConduitSpecInfo ConduitSpec { get; set; }

            public ConduitForConversion(string externalId, string externalSpec, string segmentIds)
            {
                ExternalId = externalId;
                ExternalSpec = externalSpec;

                var segmentSplit = segmentIds.Split(',');

                foreach (var segmentId in segmentSplit)
                {
                    SegmentIds.Add(Guid.Parse(segmentId));
                }

                ConduitSpec = MapToSpanEquipmentSpecification(externalSpec);
            }

            private ConduitSpecInfo MapToSpanEquipmentSpecification(string externalSpec)
            {
                // Ø 32
                if (externalSpec.StartsWith("32mm-3x10"))
                    return new ConduitSpecInfo(TestSpecifications.Multi_Ø32_3x10, GetMarkingText(externalSpec));

                // Ø40 tomrør med indhold
                if (externalSpec == "40 mm 1x10")
                    return new ConduitSpecInfo(TestSpecifications.Tomrør_Ø40_Orange, GetAdditionalStructures(externalSpec));

                if (externalSpec == "40-4x10--sort")
                    return new ConduitSpecInfo(TestSpecifications.Tomrør_Ø40_Orange, GetAdditionalStructures(externalSpec));

                if (externalSpec == "40-2x10-grøn-blå")
                    return new ConduitSpecInfo(TestSpecifications.Tomrør_Ø40_Orange, GetAdditionalStructures(externalSpec));

                if (externalSpec == "40-3x10")
                    return new ConduitSpecInfo(TestSpecifications.Tomrør_Ø40_Orange, GetMarkingText(externalSpec));

                // Ø40 multi

                if (externalSpec == "40-5x10")
                    return new ConduitSpecInfo(TestSpecifications.Multi_Ø40_5x10, GetMarkingText(externalSpec));

                if (externalSpec == "corenet 40-5x10")
                    return new ConduitSpecInfo(TestSpecifications.Multi_Ø40_5x10, GetMarkingText(externalSpec));

                if (externalSpec == "40-5x10 blå tape")
                    return new ConduitSpecInfo(TestSpecifications.Multi_Ø40_5x10, GetMarkingText(externalSpec));

                if (externalSpec == "40-5x10 blå tape rd")
                    return new ConduitSpecInfo(TestSpecifications.Multi_Ø40_5x10_Red, GetMarkingText(externalSpec));

                if (externalSpec == "40-5x10 rød tape")
                    return new ConduitSpecInfo(TestSpecifications.Multi_Ø40_5x10, GetMarkingText(externalSpec));

                if (externalSpec == "40-5x10-2sort")
                    return new ConduitSpecInfo(TestSpecifications.Multi_Ø40_5x10_BlackBlack, GetMarkingText(externalSpec));

                if (externalSpec == "40-5x10-red")
                    return new ConduitSpecInfo(TestSpecifications.Multi_Ø40_5x10_Red, GetMarkingText(externalSpec));

                if (externalSpec.StartsWith("40-6x10"))
                    return new ConduitSpecInfo(TestSpecifications.Multi_Ø40_6x10, GetMarkingText(externalSpec));

                if (externalSpec.StartsWith("corenet 40-6x10"))
                    return new ConduitSpecInfo(TestSpecifications.Multi_Ø40_6x10, GetMarkingText(externalSpec));

                if (externalSpec.StartsWith("40-12x7mm"))
                    return new ConduitSpecInfo(TestSpecifications.Multi_Ø40_12x7, GetMarkingText(externalSpec));

                // Ø50
                if (externalSpec.StartsWith("50-5x10"))
                    return new ConduitSpecInfo(TestSpecifications.Multi_Ø40_5x10, GetMarkingText(externalSpec));

                if (externalSpec == "core-acces 50-10x10")
                    return new ConduitSpecInfo(TestSpecifications.Multi_Ø50_10x10, GetMarkingText(externalSpec));

                if (externalSpec.StartsWith("50-10x10"))
                    return new ConduitSpecInfo(TestSpecifications.Multi_Ø50_10x10, GetMarkingText(externalSpec));

                if (externalSpec == "50-12x7+5x10color")
                    return new ConduitSpecInfo(TestSpecifications.Multi_Ø50_12x7_5x10, GetMarkingText(externalSpec));

                if (externalSpec == "50-12x7+5x10")
                    return new ConduitSpecInfo(TestSpecifications.Multi_Ø50_12x7_5x10_BlueYellow, GetMarkingText(externalSpec));

                if (externalSpec == "50-12x7+5x10 gr wh")
                    return new ConduitSpecInfo(TestSpecifications.Multi_Ø50_12x7_5x10_GreenWhite, GetMarkingText(externalSpec));

                if (externalSpec == "50-10x7+5x10color")
                    return new ConduitSpecInfo(TestSpecifications.Multi_Ø50_10x7_5x10, GetMarkingText(externalSpec));


                // Tomrør
                if (externalSpec == "40 mm tomt rør")
                    return new ConduitSpecInfo(TestSpecifications.Tomrør_Ø40_Orange);



                // flexrør
                if (externalSpec.StartsWith("flexrør") || externalSpec == "oe 40 mm tomt rør")
                {
                    return new ConduitSpecInfo(TestSpecifications.Flex_Ø40_Red, GetAdditionalStructures(externalSpec));
                }

                // Ø110
                if (externalSpec.StartsWith("rør 110"))
                {
                    return new ConduitSpecInfo(TestSpecifications.Tomrør_Ø110_Red, GetAdditionalStructures(externalSpec));
                }


                Log.Warning($"Don't know how to handle spec: '{externalSpec}'");

                return null;
            }

            private string GetMarkingText(string spec)
            {
                if (spec.Contains("blå tape"))
                    return "Blå tape";

                if (spec.Contains("rød tape"))
                    return "Rød tape";

                if (spec.Contains("grø tape"))
                    return "Grøn tape";

                if (spec.Contains("gul tape"))
                    return "Gul tape";

                if (spec.Contains("hvid tape"))
                    return "Hvid tape";

                if (spec.Contains("sort tape"))
                    return "Sort tape";

                return null;
            }

            private List<Guid> GetAdditionalStructures(string spec)
            {
                List<Guid> additionalStructureSpecs = new List<Guid>();

                if (spec.Contains("1bk"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Black);

                if (spec.Contains("1bl"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Blue);

                if (spec.Contains("1sl"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Silver);

                if (spec.Contains("1wh"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_White);

                if (spec.Contains("1ye"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Yellow);

                if (spec.Contains("1br"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Brown);

                if (spec.Contains("1gr"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Green);

                if (spec.Contains("1or"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Orange);

                if (spec.Contains("1rd"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Red);

                if (spec.Contains("1ro"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Pink);

                if (spec.Contains("1tu"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Turquoise);

                if (spec.Contains("1vi"))
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Violet);

                if (spec.Contains("2bl"))
                {
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Blue);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Blue);
                }

                if (spec.Contains("2ye"))
                {
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Yellow);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Yellow);
                }

                if (spec.Contains("bl+ye+bk+wh"))
                {
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Blue);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Yellow);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Black);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_White);
                }

                // Ø40 speciel
                if (spec.Contains("40 mm 1x10"))
                {
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Yellow);
                }

                if (spec.Contains("40-2x10-gron-blå"))
                {
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Green);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Blue);
                }

                if (spec.Contains("40-4x10--sort"))
                {
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Blue);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Yellow);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_White);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Green);
                }

                if (spec.Contains("40-3x10"))
                {
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Blue);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Yellow);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_White);
                }


                // Ø110

                if (spec.Contains("110-4x10-red"))
                {
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Yellow);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_White);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Green);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Red);
                }


                if (spec.Contains("110-5x10"))
                {
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Blue);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Yellow);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_White);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Green);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Red);
                }

                if (spec.Contains("110-6x10"))
                {
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Blue);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Yellow);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_White);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Green);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Red);
                    additionalStructureSpecs.Add(TestSpecifications.Ø10_Black);
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

        private class ConduitSpecInfo
        {
            public Guid SpecId { get; set; }
            public string MarkingColor { get; set; }

            public List<Guid> AditionalSpecs = new List<Guid>();

            public ConduitSpecInfo(Guid specId, string markingColor = null)
            {
                SpecId = specId;
                MarkingColor = markingColor;
            }

            public ConduitSpecInfo(Guid specId, List<Guid> aditionalSpecs)
            {
                SpecId = specId;
                AditionalSpecs = aditionalSpecs;
            }
        }
    }
}
