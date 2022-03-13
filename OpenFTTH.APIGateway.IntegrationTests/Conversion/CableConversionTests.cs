using FluentAssertions;
using FluentResults;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.Conversion;
using OpenFTTH.APIGateway.IntegrationTests.TestData;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.TestData;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.Business.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions.Ordering;

namespace OpenFTTH.APIGateway.IntegrationTests.Conversion
{

    [Order(200)]
    public class CableConversionTests
    {
        private ILoggerFactory _loggerFactory;
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;

        private static TestSpecifications _specs;

        public CableConversionTests(ILoggerFactory loggerFactory, IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _loggerFactory = loggerFactory;
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
        }


        [Fact, Order(10)]
        public async Task PlaceCableFromHH1_to_HH11_ThroughConduitThenDirectyInRouteNetwork()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var importer = new CableSpanEquipmentImporter(_loggerFactory.CreateLogger<ConduitSpanEquipmentImporter>(), _eventStore, new Settings.GeoDatabaseSetting(), _commandDispatcher, _queryDispatcher);

            Guid cableId = Guid.NewGuid();

            List<Guid> segmentIds = new List<Guid> {
                TestRouteNetwork.S2,
                TestRouteNetwork.S4,
                TestRouteNetwork.S13,
                TestRouteNetwork.S12,
            };

            List<CableConduitRel> conduitRels = new List<CableConduitRel>()
            {
                new CableConduitRel() { SpanEquipmentId = TestConduits.Conduit_5x10_HH_1_to_HH_10, InnerConduitNumber = 1 }
            };

            var placeCableResult = importer.PlaceCableSpanEquipment(null, cableId, "TEST1", TestSpecifications.FiberCable_24Fiber, segmentIds, conduitRels);

            placeCableResult.IsSuccess.Should().BeTrue();

            utilityNetwork.TryGetEquipment<SpanEquipment>(cableId, out var placedCable);


            var routeNetworkQueryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
                new GetRouteNetworkDetails(new InterestIdList() { placedCable.WalkOfInterestId })
                {
                    RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
                }
            );

            var traceQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
               new GetEquipmentDetails(new EquipmentIdList() { cableId })
               {
                   EquipmentDetailsFilter = new EquipmentDetailsFilterOptions()
                   {
                       IncludeRouteNetworkTrace = true
                   }
               }
            );


            utilityNetwork.TryGetEquipment<SpanEquipment>(TestConduits.Conduit_5x10_HH_1_to_HH_10, out var routeThroughConduit);


            // Assert

            // Check walk of interest
            var walkOfInterest = routeNetworkQueryResult.Value.Interests[placedCable.WalkOfInterestId];

            walkOfInterest.RouteNetworkElementRefs.First().Should().Be(TestRouteNetwork.HH_1);
            walkOfInterest.RouteNetworkElementRefs.Last().Should().Be(TestRouteNetwork.HH_11);

            // Check parent relationship
            placedCable.UtilityNetworkHops[0].ParentAffixes.Should().NotBeNull();
            placedCable.UtilityNetworkHops[0].ParentAffixes.Count().Should().Be(1);
            placedCable.UtilityNetworkHops[0].ParentAffixes.Single(p => p.SpanSegmentId == routeThroughConduit.SpanStructures[1].SpanSegments[0].Id).Should().NotBeNull();


        }


        [Fact, Order(11)]
        public async Task PlaceCableFromCO1_to_HH11_FirstThroughRouteNetworkDirectlyThenThroughConduit()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var importer = new CableSpanEquipmentImporter(_loggerFactory.CreateLogger<ConduitSpanEquipmentImporter>(), _eventStore, new Settings.GeoDatabaseSetting(), _commandDispatcher, _queryDispatcher);

            Guid cableId = Guid.NewGuid();

            List<Guid> segmentIds = new List<Guid> {
                TestRouteNetwork.S1,
                TestRouteNetwork.S2,
                TestRouteNetwork.S4,
                TestRouteNetwork.S13,
                TestRouteNetwork.S12,
            };

            List<CableConduitRel> conduitRels = new List<CableConduitRel>()
            {
                new CableConduitRel() { SpanEquipmentId = TestConduits.Conduit_5x10_HH_1_to_HH_10, InnerConduitNumber = 1 }
            };

            var placeCableResult = importer.PlaceCableSpanEquipment(null, cableId, "TEST1", TestSpecifications.FiberCable_24Fiber, segmentIds, conduitRels);

            placeCableResult.IsSuccess.Should().BeTrue();

            utilityNetwork.TryGetEquipment<SpanEquipment>(cableId, out var placedCable);


            var routeNetworkQueryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
                new GetRouteNetworkDetails(new InterestIdList() { placedCable.WalkOfInterestId })
                {
                    RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
                }
            );

            var traceQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
               new GetEquipmentDetails(new EquipmentIdList() { cableId })
               {
                   EquipmentDetailsFilter = new EquipmentDetailsFilterOptions()
                   {
                       IncludeRouteNetworkTrace = true
                   }
               }
            );


            utilityNetwork.TryGetEquipment<SpanEquipment>(TestConduits.Conduit_5x10_HH_1_to_HH_10, out var routeThroughConduit);


            // Assert

            // Check walk of interest
            var walkOfInterest = routeNetworkQueryResult.Value.Interests[placedCable.WalkOfInterestId];

            walkOfInterest.RouteNetworkElementRefs.First().Should().Be(TestRouteNetwork.CO_1);
            walkOfInterest.RouteNetworkElementRefs.Last().Should().Be(TestRouteNetwork.HH_11);

            // Check parent relationship
            placedCable.UtilityNetworkHops[0].ParentAffixes.Should().NotBeNull();
            placedCable.UtilityNetworkHops[0].ParentAffixes.Count().Should().Be(1);
            placedCable.UtilityNetworkHops[0].ParentAffixes.Single(p => p.SpanSegmentId == routeThroughConduit.SpanStructures[1].SpanSegments[0].Id).Should().NotBeNull();


        }


        [Fact, Order(12)]
        public async Task PlaceCableFromHH1_to_HH11_ThroughConnectedConduit()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var importer = new CableSpanEquipmentImporter(_loggerFactory.CreateLogger<ConduitSpanEquipmentImporter>(), _eventStore, new Settings.GeoDatabaseSetting(), _commandDispatcher, _queryDispatcher);

            Guid cableId = Guid.NewGuid();

            List<Guid> segmentIds = new List<Guid> {
                TestRouteNetwork.S12,
                TestRouteNetwork.S13,
                TestRouteNetwork.S4,
                TestRouteNetwork.S2,
            };

            List<CableConduitRel> conduitRels = new List<CableConduitRel>()
            {
                new CableConduitRel() { SpanEquipmentId = TestConduits.Conduit_5x10_HH_1_to_HH_10, InnerConduitNumber = 2 }, // the one
                new CableConduitRel() { SpanEquipmentId = TestConduits.Conduit_3x10_HH_10_to_HH_11, InnerConduitNumber = 2 }, // should be ignored
                new CableConduitRel() { SpanEquipmentId = Guid.NewGuid(), InnerConduitNumber = 1 } // some non existing conduit
            };

            var placeCableResult = importer.PlaceCableSpanEquipment(null, cableId, "TEST1", TestSpecifications.FiberCable_24Fiber, segmentIds, conduitRels);

            placeCableResult.IsSuccess.Should().BeTrue();

            utilityNetwork.TryGetEquipment<SpanEquipment>(cableId, out var placedCable);


            var routeNetworkQueryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
                new GetRouteNetworkDetails(new InterestIdList() { placedCable.WalkOfInterestId })
                {
                    RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
                }
            );

            var traceQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
               new GetEquipmentDetails(new EquipmentIdList() { cableId })
               {
                   EquipmentDetailsFilter = new EquipmentDetailsFilterOptions()
                   {
                       IncludeRouteNetworkTrace = true
                   }
               }
            );


            utilityNetwork.TryGetEquipment<SpanEquipment>(TestConduits.Conduit_5x10_HH_1_to_HH_10, out var routeThroughConduit1);
            utilityNetwork.TryGetEquipment<SpanEquipment>(TestConduits.Conduit_3x10_HH_10_to_HH_11, out var routeThroughConduit2);


            // Assert

            // Check walk of interest
            var walkOfInterest = routeNetworkQueryResult.Value.Interests[placedCable.WalkOfInterestId];

            walkOfInterest.RouteNetworkElementRefs.First().Should().Be(TestRouteNetwork.HH_11);
            walkOfInterest.RouteNetworkElementRefs.Last().Should().Be(TestRouteNetwork.HH_1);

            // Check parent relationship
            placedCable.UtilityNetworkHops[0].ParentAffixes.Should().NotBeNull();
            placedCable.UtilityNetworkHops[0].ParentAffixes.Count().Should().Be(2);

            placedCable.UtilityNetworkHops[0].ParentAffixes.Single(p => p.SpanSegmentId == routeThroughConduit1.SpanStructures[2].SpanSegments[0].Id).Should().NotBeNull();
            placedCable.UtilityNetworkHops[0].ParentAffixes.Single(p => p.SpanSegmentId == routeThroughConduit2.SpanStructures[2].SpanSegments[0].Id).Should().NotBeNull();


        }

        [Fact, Order(1000)]
        public void RunConversion()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var dbSettings = new Settings.GeoDatabaseSetting()
            {
                Host = "localhost",
                Database = "open-ftth",
                Username = "postgres",
                Password = "postgres",
                Port = "5432"
            };

            var importer = new CableSpanEquipmentImporter(_loggerFactory.CreateLogger<ConduitSpanEquipmentImporter>(), _eventStore, dbSettings, _commandDispatcher, _queryDispatcher);

            //importer.Run();
        }
    }
}
