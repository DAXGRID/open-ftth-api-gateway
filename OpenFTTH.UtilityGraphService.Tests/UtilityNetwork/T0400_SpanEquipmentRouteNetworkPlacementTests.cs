using OpenFTTH.CQRS;
using Xunit;
using FluentAssertions;
using FluentResults;
using OpenFTTH.RouteNetwork.API.Model;
using System;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.Tests.TestData;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.Events.Core.Infos;
using DAX.EventProcessing;
using System.Linq;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.TestData;
using Xunit.Extensions.Ordering;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Tests.UtilityNetwork
{
    [Order(400)]
    public class T0400_SpanEquipmentRouteNetworkPlacementTests
    {
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly FakeExternalEventProducer _externalEventProducer;

        public T0400_SpanEquipmentRouteNetworkPlacementTests(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = (FakeExternalEventProducer)externalEventProducer;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }

        [Fact]
        public async Task TestPlaceValidSpanEquipment_ShouldSucceed()
        {
            // Setup
            var specs = new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();

            var walkOfInterestId = Guid.NewGuid();
            var registerWalkOfInterestCommand = new RegisterWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), walkOfInterestId, new RouteNetworkElementIdList() { TestRouteNetwork.S1 });
            var registerWalkOfInterestCommandResult = _commandDispatcher.HandleAsync<RegisterWalkOfInterest, Result<RouteNetworkInterest>>(registerWalkOfInterestCommand).Result;

            var placeSpanEquipmentCommand = new PlaceSpanEquipmentInRouteNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty), Guid.NewGuid(), TestSpecifications.Multi_Ø32_3x10, registerWalkOfInterestCommandResult.Value)
            {
                NamingInfo = new NamingInfo("Hans", "Grethe"),
                MarkingInfo = new MarkingInfo() { MarkingColor = "Red", MarkingText = "ABCDE" },
                ManufacturerId = Guid.NewGuid()
            };

            // Act
            var placeSpanEquipmentResult = await _commandDispatcher.HandleAsync<PlaceSpanEquipmentInRouteNetwork, Result>(placeSpanEquipmentCommand);

            var spanEquipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new EquipmentIdList() { placeSpanEquipmentCommand.SpanEquipmentId })
            );

            // Assert
            placeSpanEquipmentResult.IsSuccess.Should().BeTrue();
            spanEquipmentQueryResult.IsSuccess.Should().BeTrue();
            spanEquipmentQueryResult.Value.Should().NotBeNull();
            spanEquipmentQueryResult.Value.SpanEquipment.Should().NotBeNull();

#pragma warning disable CS8602 // Dereference of a possibly null reference.
            spanEquipmentQueryResult.Value.SpanEquipment[placeSpanEquipmentCommand.SpanEquipmentId].Id.Should().Be(placeSpanEquipmentCommand.SpanEquipmentId);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            spanEquipmentQueryResult.Value.SpanEquipment[placeSpanEquipmentCommand.SpanEquipmentId].SpecificationId.Should().Be(placeSpanEquipmentCommand.SpanEquipmentSpecificationId);
            spanEquipmentQueryResult.Value.SpanEquipment[placeSpanEquipmentCommand.SpanEquipmentId].WalkOfInterestId.Should().Be(placeSpanEquipmentCommand.Interest.Id);
            spanEquipmentQueryResult.Value.SpanEquipment[placeSpanEquipmentCommand.SpanEquipmentId].NamingInfo.Should().BeEquivalentTo(placeSpanEquipmentCommand.NamingInfo);
            spanEquipmentQueryResult.Value.SpanEquipment[placeSpanEquipmentCommand.SpanEquipmentId].MarkingInfo.Should().BeEquivalentTo(placeSpanEquipmentCommand.MarkingInfo);
            spanEquipmentQueryResult.Value.SpanEquipment[placeSpanEquipmentCommand.SpanEquipmentId].ManufacturerId.Should().Be(placeSpanEquipmentCommand.ManufacturerId);

            spanEquipmentQueryResult.Value.SpanEquipment[placeSpanEquipmentCommand.SpanEquipmentId].SpanStructures.Length.Should().Be(4);

            // Check that span segments are correctly populated
            for (int structureIndex = 0; structureIndex < spanEquipmentQueryResult.Value.SpanEquipment[placeSpanEquipmentCommand.SpanEquipmentId].SpanStructures.Length; structureIndex++)
            {
                spanEquipmentQueryResult.Value.SpanEquipment[placeSpanEquipmentCommand.SpanEquipmentId].SpanStructures[structureIndex].SpanSegments.Length.Should().Be(1);
                spanEquipmentQueryResult.Value.SpanEquipment[placeSpanEquipmentCommand.SpanEquipmentId].SpanStructures[structureIndex].SpanSegments[0].FromTerminalId.Should().BeEmpty();
                spanEquipmentQueryResult.Value.SpanEquipment[placeSpanEquipmentCommand.SpanEquipmentId].SpanStructures[structureIndex].SpanSegments[0].ToTerminalId.Should().BeEmpty();
                spanEquipmentQueryResult.Value.SpanEquipment[placeSpanEquipmentCommand.SpanEquipmentId].SpanStructures[structureIndex].SpanSegments[0].FromNodeOfInterestIndex.Should().Be(0);
                spanEquipmentQueryResult.Value.SpanEquipment[placeSpanEquipmentCommand.SpanEquipmentId].SpanStructures[structureIndex].SpanSegments[0].ToNodeOfInterestIndex.Should().Be(1);
                spanEquipmentQueryResult.Value.SpanEquipment[placeSpanEquipmentCommand.SpanEquipmentId].SpanStructures[structureIndex].SpanSegments[0].Id.Should().NotBeEmpty();
            }

            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == placeSpanEquipmentCommand.SpanEquipmentId)));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.CO_1);
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.S1);
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.HH_1);
        }

        [Fact]
        public async Task TestQuerySpanEquipmentByInterestId_ShouldSucceed()
        {
            // Setup
            var specs = new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();

            var walkOfInterest = new RouteNetworkInterest(Guid.NewGuid(), RouteNetworkInterestKindEnum.WalkOfInterest, new RouteNetworkElementIdList() { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() });

            var placeSpanEquipmentCommand = new PlaceSpanEquipmentInRouteNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty), Guid.NewGuid(), TestSpecifications.Multi_Ø32_3x10, walkOfInterest);

            // Act
            var placeSpanEquipmentResult = await _commandDispatcher.HandleAsync<PlaceSpanEquipmentInRouteNetwork, Result>(placeSpanEquipmentCommand);

            var spanEquipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new InterestIdList() { walkOfInterest.Id })
            );

            // Assert
            placeSpanEquipmentResult.IsSuccess.Should().BeTrue();
            spanEquipmentQueryResult.IsSuccess.Should().BeTrue();

#pragma warning disable CS8602 // Dereference of a possibly null reference.
            spanEquipmentQueryResult.Value.SpanEquipment[placeSpanEquipmentCommand.SpanEquipmentId].Id.Should().Be(placeSpanEquipmentCommand.SpanEquipmentId);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        }

        [Fact]
        public async Task TestPlaceTwoSpanEquipmentWithSameId_SecondOneShouldFail()
        {
            var walkOfInterest = new RouteNetworkInterest(Guid.NewGuid(), RouteNetworkInterestKindEnum.WalkOfInterest, new RouteNetworkElementIdList() { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() });

            var placeSpanEquipmentCommand = new PlaceSpanEquipmentInRouteNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty), Guid.NewGuid(), TestSpecifications.Multi_Ø32_3x10, walkOfInterest);

            // Act
            var placeSpanEquipmentResult = await _commandDispatcher.HandleAsync<PlaceSpanEquipmentInRouteNetwork, Result>(placeSpanEquipmentCommand);
            var placeSpanEquipmentResult2 = await _commandDispatcher.HandleAsync<PlaceSpanEquipmentInRouteNetwork, Result>(placeSpanEquipmentCommand);

            // Assert
            placeSpanEquipmentResult.IsSuccess.Should().BeTrue();
            placeSpanEquipmentResult2.IsSuccess.Should().BeFalse();
        }
    }
}
