using DAX.EventProcessing;
using FluentAssertions;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.TestData;
using OpenFTTH.Tests.Util;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.Business.Graph;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions.Ordering;

#nullable disable

namespace OpenFTTH.UtilityGraphService.Tests.UtilityNetwork
{
    [Order(4000)]
    public class T4000_SpanEquipmentUtilityNetworkPlacementTests
    {
        private readonly IEventStore _eventStore;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly FakeExternalEventProducer _externalEventProducer;

        private readonly UtilityNetworkBuilder _utilityNetworkBuilder;

        public T4000_SpanEquipmentUtilityNetworkPlacementTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = (FakeExternalEventProducer)externalEventProducer;
            _utilityNetworkBuilder = new UtilityNetworkBuilder(_eventStore, _commandDispatcher, _queryDispatcher);

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public async Task TestPlaceSpanEquipmentUsingOneHopFromCO1ToCC1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            // The span equipment/segment where to route the child span equipment
            var routeThroughSpanEquipmentId = TestUtilityNetwork.MultiConduit_5x10_CO_1_to_HH_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(routeThroughSpanEquipmentId, out var routeThoughSpanEquipment);

            var routeThroughSpanSegmentId = routeThoughSpanEquipment.SpanStructures[5].SpanSegments[0].Id;


            // The span equipment/segment that the start span segment is connected to
            var routeThroughConnectedSpanEquipmentId = TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10;

            utilityNetwork.TryGetEquipment<SpanEquipment>(routeThroughConnectedSpanEquipmentId, out var routeThroughConnectedSpanEquipment);

            var routeThroughConnectedSpanSegmentId = routeThroughConnectedSpanEquipment.SpanStructures[5].SpanSegments[0].Id;


            // Setup command
            var specs = new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();

            var routingHops = new RoutingHop[]
            {
                new RoutingHop(TestRouteNetwork.CO_1, routeThroughSpanSegmentId)
            };

            var placeSpanEquipmentCommand = new PlaceSpanEquipmentInUtilityNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty), Guid.NewGuid(), TestSpecifications.FiberCable_72Fiber, routingHops)
            {
                NamingInfo = new NamingInfo("K69373563", null),
                MarkingInfo = new MarkingInfo() { MarkingColor = "Red", MarkingText = "ABCDE" },
                ManufacturerId = Guid.NewGuid()
            };

            // Act
            var placeSpanEquipmentResult = await _commandDispatcher.HandleAsync<PlaceSpanEquipmentInUtilityNetwork, Result>(placeSpanEquipmentCommand);

            utilityNetwork.TryGetEquipment<SpanEquipment>(placeSpanEquipmentCommand.SpanEquipmentId, out var placedSpanEquipment);

            var routeNetworkQueryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
              new GetRouteNetworkDetails(new InterestIdList() { placedSpanEquipment.WalkOfInterestId })
              {
                  RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
              }
            );

            var traceQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
               new GetEquipmentDetails(new EquipmentIdList() { placeSpanEquipmentCommand.SpanEquipmentId })
               {
                   EquipmentDetailsFilter = new EquipmentDetailsFilterOptions()
                   {
                       IncludeRouteNetworkTrace = true
                   }
               }
            );

            // Assert
            placeSpanEquipmentResult.IsSuccess.Should().BeTrue();
            routeNetworkQueryResult.IsSuccess.Should().BeTrue();
            traceQueryResult.IsSuccess.Should().BeTrue();

            // Check walk of interest
            var walkOfInterest = routeNetworkQueryResult.Value.Interests[placedSpanEquipment.WalkOfInterestId];

            walkOfInterest.RouteNetworkElementRefs.First().Should().Be(TestRouteNetwork.CO_1);
            walkOfInterest.RouteNetworkElementRefs.Last().Should().Be(TestRouteNetwork.CC_1);

            // Check that cable contains one hop
            placedSpanEquipment.UtilityNetworkHops.Should().NotBeNull();
            placedSpanEquipment.UtilityNetworkHops.Count().Should().Be(1);

            // Check hop from and to node id
            placedSpanEquipment.UtilityNetworkHops[0].FromNodeId.Should().Be(TestRouteNetwork.CO_1);
            placedSpanEquipment.UtilityNetworkHops[0].ToNodeId.Should().Be(TestRouteNetwork.CC_1);

            // Check conduit parent segment ids
            placedSpanEquipment.UtilityNetworkHops[0].ParentAffixes.Count().Should().Be(2);
            placedSpanEquipment.UtilityNetworkHops[0].ParentAffixes.Single(p => p.SpanSegmentId == routeThroughSpanSegmentId).Should().NotBeNull();
            placedSpanEquipment.UtilityNetworkHops[0].ParentAffixes.Single(p => p.SpanSegmentId == routeThroughConnectedSpanSegmentId).Should().NotBeNull();

            // Check that trace only include outer jacket of cable
            traceQueryResult.Value.RouteNetworkTraces.Count().Should().Be(1);


        }


        [Fact, Order(3)]
        public async Task TestPlaceSpanEquipmentUsingThreeHopFromCO1ToHH1ToHH10ToCC1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var routeThroughSpanEquipment1Id = TestUtilityNetwork.MultiConduit_Ø110_CO_1_to_HH_1;
            var routeThroughSpanEquipment2Id = TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10;

            utilityNetwork.TryGetEquipment<SpanEquipment>(routeThroughSpanEquipment1Id, out var routeThoughSpanEquipment1);
            utilityNetwork.TryGetEquipment<SpanEquipment>(routeThroughSpanEquipment2Id, out var routeThoughSpanEquipment2);

            var routeThroughSpanSegment1Id = routeThoughSpanEquipment1.SpanStructures[0].SpanSegments[0].Id;
            var routeThroughSpanSegment2Id = routeThoughSpanEquipment2.SpanStructures[6].SpanSegments[0].Id;
            var routeThroughSpanSegment3Id = routeThoughSpanEquipment2.SpanStructures[5].SpanSegments[1].Id;

            // Setup
            var specs = new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();

            // First we go from CO_1 to HH_1 through the Ø110 to HH_1, then from HH_1 to H_10 through subcoundit 6 (that has not been cut), then back to CC_1 via subconduit 5 that has been cut in CC_1
            var routingHops = new RoutingHop[]
            {
                new RoutingHop(TestRouteNetwork.CO_1, routeThroughSpanSegment1Id),
                new RoutingHop(TestRouteNetwork.HH_1, routeThroughSpanSegment2Id),
                new RoutingHop(TestRouteNetwork.HH_10, routeThroughSpanSegment3Id)
            };

            var placeSpanEquipmentCommand = new PlaceSpanEquipmentInUtilityNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty), Guid.NewGuid(), TestSpecifications.FiberCable_216Fiber, routingHops)
            {
                NamingInfo = new NamingInfo("K666", null),
                MarkingInfo = new MarkingInfo() { MarkingColor = "Red", MarkingText = "ABCDE" },
                ManufacturerId = Guid.NewGuid()
            };

            // Act
            var placeSpanEquipmentResult = await _commandDispatcher.HandleAsync<PlaceSpanEquipmentInUtilityNetwork, Result>(placeSpanEquipmentCommand);

            utilityNetwork.TryGetEquipment<SpanEquipment>(placeSpanEquipmentCommand.SpanEquipmentId, out var placedSpanEquipment);

            var routeNetworkQueryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
              new GetRouteNetworkDetails(new InterestIdList() { placedSpanEquipment.WalkOfInterestId })
              {
                  RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
              }
            );

            // Assert
            placeSpanEquipmentResult.IsSuccess.Should().BeTrue();
            routeNetworkQueryResult.IsSuccess.Should().BeTrue();

            var walkOfInterest = routeNetworkQueryResult.Value.Interests[placedSpanEquipment.WalkOfInterestId];

            walkOfInterest.RouteNetworkElementRefs.Count.Should().Be(11);

            walkOfInterest.RouteNetworkElementRefs.First().Should().Be(TestRouteNetwork.CO_1);
            walkOfInterest.RouteNetworkElementRefs.Last().Should().Be(TestRouteNetwork.CC_1);

            // Check that we go forth and backwards between CC_1 and HH_10 (S13)
            walkOfInterest.RouteNetworkElementRefs.Count(e => e == TestRouteNetwork.CC_1).Should().Be(2);
            walkOfInterest.RouteNetworkElementRefs.Count(e => e == TestRouteNetwork.S13).Should().Be(2);


            placedSpanEquipment.UtilityNetworkHops.Should().NotBeNull();
            placedSpanEquipment.UtilityNetworkHops.Count().Should().Be(3);

            // Check conduit parent relationship
            placedSpanEquipment.UtilityNetworkHops[0].ParentAffixes.Single(p => p.SpanSegmentId == routeThroughSpanSegment1Id && p.Direction == SpanEquipmentAffixDirectionEnum.Forward).Should().NotBeNull();
            placedSpanEquipment.UtilityNetworkHops[1].ParentAffixes.Single(p => p.SpanSegmentId == routeThroughSpanSegment2Id && p.Direction == SpanEquipmentAffixDirectionEnum.Forward).Should().NotBeNull();
            placedSpanEquipment.UtilityNetworkHops[2].ParentAffixes.Single(p => p.SpanSegmentId == routeThroughSpanSegment3Id && p.Direction == SpanEquipmentAffixDirectionEnum.Backward).Should().NotBeNull();

            // Check hop from and to node id
            placedSpanEquipment.UtilityNetworkHops[0].FromNodeId.Should().Be(TestRouteNetwork.CO_1);
            placedSpanEquipment.UtilityNetworkHops[0].ToNodeId.Should().Be(TestRouteNetwork.HH_1);
            placedSpanEquipment.UtilityNetworkHops[1].FromNodeId.Should().Be(TestRouteNetwork.HH_1);
            placedSpanEquipment.UtilityNetworkHops[1].ToNodeId.Should().Be(TestRouteNetwork.HH_10);
            placedSpanEquipment.UtilityNetworkHops[2].FromNodeId.Should().Be(TestRouteNetwork.HH_10);
            placedSpanEquipment.UtilityNetworkHops[2].ToNodeId.Should().Be(TestRouteNetwork.CC_1);
        }

        [Fact, Order(4)]
        public async Task TestPlaceSpanEquipmentUsingOneParentHopAndOneRouteHop_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var routeThroughSpanEquipment1Id = TestUtilityNetwork.MultiConduit_Ø110_CO_1_to_HH_1;
            var routeThroughSpanEquipment2Id = TestUtilityNetwork.FlexConduit_40_Red_HH_2_to_FP_2;

            utilityNetwork.TryGetEquipment<SpanEquipment>(routeThroughSpanEquipment1Id, out var routeThoughSpanEquipment1);

            // Setup
            var specs = new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();

            // First we go from CO_1 to HH_1 through the Ø110 to HH_1, then in the route network to HH_2, then in conduit to FP_2, then in route network back to HH_2
            var routingHops = new RoutingHop[]
            {
                new RoutingHop(TestRouteNetwork.CO_1, routeThroughSpanEquipment1Id, 0),
                new RoutingHop(new Guid[] { TestRouteNetwork.HH_1, TestRouteNetwork.S2, TestRouteNetwork.HH_2 }),
                new RoutingHop(TestRouteNetwork.HH_2, routeThroughSpanEquipment2Id, 0),
                new RoutingHop(new Guid[] { TestRouteNetwork.FP_2, TestRouteNetwork.S3, TestRouteNetwork.HH_2 }),
            };

            var placeSpanEquipmentCommand = new PlaceSpanEquipmentInUtilityNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty), Guid.NewGuid(), TestSpecifications.FiberCable_96Fiber, routingHops)
            {
                NamingInfo = new NamingInfo("1u-1r-1u-1r", "Grethe"),
                MarkingInfo = new MarkingInfo() { MarkingColor = "Red", MarkingText = "Red stripe" },
                ManufacturerId = Guid.NewGuid()
            };

            // Act
            var placeSpanEquipmentResult = await _commandDispatcher.HandleAsync<PlaceSpanEquipmentInUtilityNetwork, Result>(placeSpanEquipmentCommand);

            utilityNetwork.TryGetEquipment<SpanEquipment>(placeSpanEquipmentCommand.SpanEquipmentId, out var placedSpanEquipment);

            var routeNetworkQueryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
              new GetRouteNetworkDetails(new InterestIdList() { placedSpanEquipment.WalkOfInterestId })
              {
                  RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
              }
            );

            // Assert
            placeSpanEquipmentResult.IsSuccess.Should().BeTrue();
            routeNetworkQueryResult.IsSuccess.Should().BeTrue();

            var walkOfInterest = routeNetworkQueryResult.Value.Interests[placedSpanEquipment.WalkOfInterestId];

            walkOfInterest.RouteNetworkElementRefs.Count.Should().Be(9);

            walkOfInterest.RouteNetworkElementRefs.First().Should().Be(TestRouteNetwork.CO_1);
            walkOfInterest.RouteNetworkElementRefs.Last().Should().Be(TestRouteNetwork.HH_2);

            // Check parent relationship
            placedSpanEquipment.UtilityNetworkHops.Should().NotBeNull();
            placedSpanEquipment.UtilityNetworkHops.Count().Should().Be(2);
            placedSpanEquipment.UtilityNetworkHops[0].ParentAffixes.Single(p => p.SpanSegmentId == routeThoughSpanEquipment1.SpanStructures[0].SpanSegments[0].Id && p.Direction == SpanEquipmentAffixDirectionEnum.Forward).Should().NotBeNull();
        }

        [Fact, Order(5)]
        public async Task TestPlaceSpanEquipmentUsingOneHopFromCO1ToSP1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            // The span equipment/segment where to route the child span equipment
            var routeThroughSpanEquipmentId = TestUtilityNetwork.MultiConduit_5x10_CO_1_to_HH_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(routeThroughSpanEquipmentId, out var routeThoughSpanEquipment);

            var routeThroughSpanSegmentId = routeThoughSpanEquipment.SpanStructures[3].SpanSegments[0].Id;


            // Setup command
            var specs = new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();

            var routingHops = new RoutingHop[]
            {
                new RoutingHop(TestRouteNetwork.CO_1, routeThroughSpanSegmentId)
            };

            var placeSpanEquipmentCommand = new PlaceSpanEquipmentInUtilityNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty), Guid.NewGuid(), TestSpecifications.FiberCable_12Fiber, routingHops)
            {
                NamingInfo = new NamingInfo("K667", null),
                MarkingInfo = new MarkingInfo() { MarkingColor = "Red", MarkingText = "ABCDE" },
                ManufacturerId = Guid.NewGuid()
            };

            // Act
            var placeSpanEquipmentResult = await _commandDispatcher.HandleAsync<PlaceSpanEquipmentInUtilityNetwork, Result>(placeSpanEquipmentCommand);

            utilityNetwork.TryGetEquipment<SpanEquipment>(placeSpanEquipmentCommand.SpanEquipmentId, out var placedSpanEquipment);

            var routeNetworkQueryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
              new GetRouteNetworkDetails(new InterestIdList() { placedSpanEquipment.WalkOfInterestId })
              {
                  RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
              }
            );

            var traceQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
               new GetEquipmentDetails(new EquipmentIdList() { placeSpanEquipmentCommand.SpanEquipmentId })
               {
                   EquipmentDetailsFilter = new EquipmentDetailsFilterOptions()
                   {
                       IncludeRouteNetworkTrace = true
                   }
               }
            );

            // Assert
            placeSpanEquipmentResult.IsSuccess.Should().BeTrue();
            routeNetworkQueryResult.IsSuccess.Should().BeTrue();
            traceQueryResult.IsSuccess.Should().BeTrue();

            // Check walk of interest
            var walkOfInterest = routeNetworkQueryResult.Value.Interests[placedSpanEquipment.WalkOfInterestId];

            walkOfInterest.RouteNetworkElementRefs.First().Should().Be(TestRouteNetwork.CO_1);
            walkOfInterest.RouteNetworkElementRefs.Last().Should().Be(TestRouteNetwork.SP_1);
        }


        [Fact, Order(6)]
        public async Task TestPlaceCustomerCableCC1_to_SDU1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();


            // Setup command
            var specs = new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();

            var routingHops = new RoutingHop[]
            {
                new RoutingHop(new Guid[] { TestRouteNetwork.S5, TestRouteNetwork.S6, TestRouteNetwork.S7})
            };

            var placeSpanEquipmentCommand = new PlaceSpanEquipmentInUtilityNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty), Guid.NewGuid(), TestSpecifications.FiberCable_2Fiber, routingHops)
            {
                NamingInfo = new NamingInfo("K12345678", null),
                MarkingInfo = new MarkingInfo() { MarkingColor = "Red", MarkingText = "ABCDE" },
                ManufacturerId = Guid.NewGuid()
            };

            // Act
            var placeSpanEquipmentResult = await _commandDispatcher.HandleAsync<PlaceSpanEquipmentInUtilityNetwork, Result>(placeSpanEquipmentCommand);

            // Assert
            placeSpanEquipmentResult.IsSuccess.Should().BeTrue();
        }



        [Fact, Order(10)]
        public async Task TestMovingCableFromRouteNetworkInto_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var routeThroughSpanEquipment1Id = TestUtilityNetwork.MultiConduit_10x10_HH_1_to_HH_10;

            utilityNetwork.TryGetEquipment<SpanEquipment>(routeThroughSpanEquipment1Id, out var routeThoughSpanEquipment1);

            // Setup
            var specs = new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();

            // First place in route network from HH_1 to HH_10
            var routingHops = new RoutingHop[]
            {
                new RoutingHop(
                    new Guid[] { TestRouteNetwork.HH_1, TestRouteNetwork.S2, TestRouteNetwork.HH_2, TestRouteNetwork.S4, TestRouteNetwork.CC_1, TestRouteNetwork.S13, TestRouteNetwork.HH_10 }
                ),
            };

            var placeSpanEquipmentCommand = new PlaceSpanEquipmentInUtilityNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty), Guid.NewGuid(), TestSpecifications.FiberCable_288Fiber, routingHops)
            {
                NamingInfo = new NamingInfo("K12345678", null),
                ManufacturerId = Guid.NewGuid()
            };


            // Act
            var placeSpanEquipmentResult = await _commandDispatcher.HandleAsync<PlaceSpanEquipmentInUtilityNetwork, Result>(placeSpanEquipmentCommand);
            utilityNetwork.TryGetEquipment<SpanEquipment>(placeSpanEquipmentCommand.SpanEquipmentId, out var placedSpanEquipment);


            var affixtCommand = new AffixSpanEquipmentToParent(Guid.NewGuid(), new UserContext("test", Guid.Empty), TestRouteNetwork.CC_1, placedSpanEquipment.SpanStructures[0].SpanSegments[0].Id, routeThoughSpanEquipment1.SpanStructures[5].SpanSegments[0].Id);

            var affixCommandResult = await _commandDispatcher.HandleAsync<AffixSpanEquipmentToParent, Result>(affixtCommand);

            utilityNetwork.TryGetEquipment<SpanEquipment>(placeSpanEquipmentCommand.SpanEquipmentId, out var movedSpanEquipment);


            var routeNetworkQueryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
              new GetRouteNetworkDetails(new InterestIdList() { placedSpanEquipment.WalkOfInterestId })
              {
                  RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
              }
            );

            // Assert
            placeSpanEquipmentResult.IsSuccess.Should().BeTrue();
            affixCommandResult.IsSuccess.Should().BeTrue();
            routeNetworkQueryResult.IsSuccess.Should().BeTrue();

            var walkOfInterest = routeNetworkQueryResult.Value.Interests[placedSpanEquipment.WalkOfInterestId];

            walkOfInterest.RouteNetworkElementRefs.First().Should().Be(TestRouteNetwork.HH_1);
            walkOfInterest.RouteNetworkElementRefs.Last().Should().Be(TestRouteNetwork.HH_10);

            // Check parent relationship
            movedSpanEquipment.UtilityNetworkHops.Count().Should().Be(1);
        }


        [Fact, Order(100)]
        public async Task TestPlaceSpanEquipmentRouteByIndexFromNodeWithNoConduitEnds_ShouldFail()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var routeThroughSpanEquipment1Id = TestUtilityNetwork.MultiConduit_10x10_HH_1_to_HH_10;

            utilityNetwork.TryGetEquipment<SpanEquipment>(routeThroughSpanEquipment1Id, out var routeThoughSpanEquipment1);

            // Setup
            var specs = new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();

            // We start in HH_2 where the parent span equipment is not cut
            var routingHops = new RoutingHop[]
            {
                new RoutingHop(TestRouteNetwork.HH_2, routeThroughSpanEquipment1Id, 0),
            };

            var placeSpanEquipmentCommand = new PlaceSpanEquipmentInUtilityNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty), Guid.NewGuid(), TestSpecifications.Multi_Ø32_3x10, routingHops)
            {
                NamingInfo = new NamingInfo("Hans", "Grethe"),
                MarkingInfo = new MarkingInfo() { MarkingColor = "Red", MarkingText = "ABCDE" },
                ManufacturerId = Guid.NewGuid()
            };

            // Act
            var placeSpanEquipmentResult = await _commandDispatcher.HandleAsync<PlaceSpanEquipmentInUtilityNetwork, Result>(placeSpanEquipmentCommand);

            // Assert
            placeSpanEquipmentResult.IsFailed.Should().BeTrue();
            ((PlaceSpanEquipmentInUtilityNetworkError)placeSpanEquipmentResult.Errors.First()).Code.Should().Be(PlaceSpanEquipmentInUtilityNetworkErrorCodes.SPAN_SEGMENT_NOT_RELATED_TO_ROUTE_NODE);
        }

        [Fact, Order(101)]
        public async Task TestPlaceSpanEquipmentWithInvalidRouteNetworkWalk_ShouldFail()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var routeThroughSpanEquipment1Id = TestUtilityNetwork.MultiConduit_Ø110_CO_1_to_HH_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(routeThroughSpanEquipment1Id, out var routeThoughSpanEquipment1);

            // Setup
            var specs = new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();

            // First we go from CO_1 to HH_1 through the Ø110 to HH_1, then in the route network to HH_2
            var routingHops = new RoutingHop[]
            {
                new RoutingHop(TestRouteNetwork.CO_1, routeThroughSpanEquipment1Id, 0),
                new RoutingHop(
                    new Guid[] { TestRouteNetwork.HH_1, TestRouteNetwork.S2 } // Missing end in walk
                ),
            };

            var placeSpanEquipmentCommand = new PlaceSpanEquipmentInUtilityNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty), Guid.NewGuid(), TestSpecifications.Multi_Ø32_3x10, routingHops)
            {
                NamingInfo = new NamingInfo("Hans", "Grethe"),
                MarkingInfo = new MarkingInfo() { MarkingColor = "Red", MarkingText = "ABCDE" },
                ManufacturerId = Guid.NewGuid()
            };

            // Act
            var placeSpanEquipmentResult = await _commandDispatcher.HandleAsync<PlaceSpanEquipmentInUtilityNetwork, Result>(placeSpanEquipmentCommand);

            utilityNetwork.TryGetEquipment<SpanEquipment>(placeSpanEquipmentCommand.SpanEquipmentId, out var placedSpanEquipment);

            // Assert
            placeSpanEquipmentResult.IsFailed.Should().BeTrue();
        }

        [Fact, Order(102)]
        public async Task TestPlaceSpanEquipmenWithHoleBetweenHops_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var routeThroughSpanEquipment1Id = TestUtilityNetwork.MultiConduit_Ø110_CO_1_to_HH_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(routeThroughSpanEquipment1Id, out var routeThoughSpanEquipment1);

            // Setup
            var specs = new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();

            // First we go from CO_1 to HH_1 through the Ø110 to HH_1, then in the route network to HH_2
            var routingHops = new RoutingHop[]
            {
                new RoutingHop(TestRouteNetwork.CO_1, routeThroughSpanEquipment1Id, 0),
                new RoutingHop(
                    new Guid[] { TestRouteNetwork.HH_2, TestRouteNetwork.S4, TestRouteNetwork.CC_1 }
                ),
            };

            var placeSpanEquipmentCommand = new PlaceSpanEquipmentInUtilityNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty), Guid.NewGuid(), TestSpecifications.Multi_Ø32_3x10, routingHops)
            {
                NamingInfo = new NamingInfo("Hans", "Grethe"),
                MarkingInfo = new MarkingInfo() { MarkingColor = "Red", MarkingText = "ABCDE" },
                ManufacturerId = Guid.NewGuid()
            };

            // Act
            var placeSpanEquipmentResult = await _commandDispatcher.HandleAsync<PlaceSpanEquipmentInUtilityNetwork, Result>(placeSpanEquipmentCommand);

            utilityNetwork.TryGetEquipment<SpanEquipment>(placeSpanEquipmentCommand.SpanEquipmentId, out var placedSpanEquipment);

            // Assert
            placeSpanEquipmentResult.IsFailed.Should().BeTrue();
            ((PlaceSpanEquipmentInUtilityNetworkError)placeSpanEquipmentResult.Errors.First()).Code.Should().Be(PlaceSpanEquipmentInUtilityNetworkErrorCodes.ERROR_CALCULATING_WALK);
        }



        [Fact, Order(102)]
        public async Task TestPlaceSpanEquipmentRouteBySpanEquipmentFromNodeWithNoConduitEnds_ShouldFail()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var routeThroughSpanEquipment1Id = TestUtilityNetwork.MultiConduit_10x10_HH_1_to_HH_10;

            utilityNetwork.TryGetEquipment<SpanEquipment>(routeThroughSpanEquipment1Id, out var routeThoughSpanEquipment);

            var routeThroughSpanSegmentId = routeThoughSpanEquipment.SpanStructures[6].SpanSegments[0].Id;

            // Setup
            var specs = new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();

            // We start in HH_2 where the parent span equipment is not cut
            var routingHops = new RoutingHop[]
            {
                new RoutingHop(TestRouteNetwork.HH_2, routeThroughSpanSegmentId),
            };

            var placeSpanEquipmentCommand = new PlaceSpanEquipmentInUtilityNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty), Guid.NewGuid(), TestSpecifications.Multi_Ø32_3x10, routingHops)
            {
                NamingInfo = new NamingInfo("Hans", "Grethe"),
                MarkingInfo = new MarkingInfo() { MarkingColor = "Red", MarkingText = "ABCDE" },
                ManufacturerId = Guid.NewGuid()
            };

            // Act
            var placeSpanEquipmentResult = await _commandDispatcher.HandleAsync<PlaceSpanEquipmentInUtilityNetwork, Result>(placeSpanEquipmentCommand);

            // Assert
            placeSpanEquipmentResult.IsFailed.Should().BeTrue();
            ((PlaceSpanEquipmentInUtilityNetworkError)placeSpanEquipmentResult.Errors.First()).Code.Should().Be(PlaceSpanEquipmentInUtilityNetworkErrorCodes.SPAN_SEGMENT_NOT_RELATED_TO_ROUTE_NODE);

        }


        [Fact, Order(104)]
        public async Task TestPlaceSpanEquipmenInHopThatRunsForthAndBackInTheSameRouteSegment_ShouldSucceed()
        {
            // Connect sub conduit 1 and 2 of the same multi conduit in CC_1
            _utilityNetworkBuilder.ConnectTwoSpanSegments(
                nodeId: TestRouteNetwork.CC_1,
                fromSpanEquipmentId: TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10,
                fromSpanStructureIndex: 1,
                fromSpanSegmentIndex: 1,
                toSpanEquipmentId: TestUtilityNetwork.CustomerConduit_HH_11_to_CC_1,
                toSpanStructureIndex: 0,
                toSpanSegmentIndex: 0
            );

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var routeThroughSpanEquipment1Id = TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10;
            utilityNetwork.TryGetEquipment<SpanEquipment>(routeThroughSpanEquipment1Id, out var routeThoughSpanEquipment1);
            var routeThroughSpanSegment1Id = routeThoughSpanEquipment1.SpanStructures[1].SpanSegments[1].Id;


            var routeThroughSpanEquipment2Id = TestUtilityNetwork.CustomerConduit_HH_11_to_CC_1;
            utilityNetwork.TryGetEquipment<SpanEquipment>(routeThroughSpanEquipment2Id, out var routeThoughSpanEquipment2);
            var routeThroughSpanSegment2Id = routeThoughSpanEquipment2.SpanStructures[0].SpanSegments[0].Id;


            // Setup
            var specs = new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();

            // blow from HH_10 through sub conduit 1, which goes to CC_1 and back to HH_11 via. HH_10 (in the customer conduit)
            var routingHops = new RoutingHop[]
            {
                new RoutingHop(TestRouteNetwork.HH_10, routeThroughSpanSegment1Id),
            };

            var placeSpanEquipmentCommand = new PlaceSpanEquipmentInUtilityNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty), Guid.NewGuid(), TestSpecifications.Multi_Ø32_3x10, routingHops)
            {
                NamingInfo = new NamingInfo("Hans", "Grethe"),
                MarkingInfo = new MarkingInfo() { MarkingColor = "Red", MarkingText = "ABCDE" },
                ManufacturerId = Guid.NewGuid()
            };

            // Act
            var placeSpanEquipmentResult = await _commandDispatcher.HandleAsync<PlaceSpanEquipmentInUtilityNetwork, Result>(placeSpanEquipmentCommand);

            utilityNetwork.TryGetEquipment<SpanEquipment>(placeSpanEquipmentCommand.SpanEquipmentId, out var placedSpanEquipment);

            var routeNetworkQueryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
              new GetRouteNetworkDetails(new InterestIdList() { placedSpanEquipment.WalkOfInterestId })
              {
                  RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
              }
            );

            // Assert
            placeSpanEquipmentResult.IsSuccess.Should().BeTrue();

            // Check walk of interest
            var walkOfInterest = routeNetworkQueryResult.Value.Interests[placedSpanEquipment.WalkOfInterestId];

            walkOfInterest.RouteNetworkElementRefs.Count.Should().Be(7);
            walkOfInterest.RouteNetworkElementRefs[0].Should().Be(TestRouteNetwork.HH_10);
            walkOfInterest.RouteNetworkElementRefs[1].Should().Be(TestRouteNetwork.S13);
            walkOfInterest.RouteNetworkElementRefs[2].Should().Be(TestRouteNetwork.CC_1);
            walkOfInterest.RouteNetworkElementRefs[3].Should().Be(TestRouteNetwork.S13);
            walkOfInterest.RouteNetworkElementRefs[4].Should().Be(TestRouteNetwork.HH_10);
            walkOfInterest.RouteNetworkElementRefs[5].Should().Be(TestRouteNetwork.S12);
            walkOfInterest.RouteNetworkElementRefs[6].Should().Be(TestRouteNetwork.HH_11);

            placedSpanEquipment.UtilityNetworkHops.Should().NotBeNull();
            placedSpanEquipment.UtilityNetworkHops.Count().Should().Be(1);

            // Check hop from and to node id
            placedSpanEquipment.UtilityNetworkHops[0].FromNodeId.Should().Be(TestRouteNetwork.HH_10);
            placedSpanEquipment.UtilityNetworkHops[0].ToNodeId.Should().Be(TestRouteNetwork.HH_11);

            // Check span segment parents
            placedSpanEquipment.UtilityNetworkHops[0].ParentAffixes.Count().Should().Be(2);
            placedSpanEquipment.UtilityNetworkHops[0].ParentAffixes[0].SpanSegmentId.Should().Be(routeThroughSpanSegment1Id);
            placedSpanEquipment.UtilityNetworkHops[0].ParentAffixes[1].SpanSegmentId.Should().Be(routeThroughSpanSegment2Id);
        }
    }
}

#nullable enable
