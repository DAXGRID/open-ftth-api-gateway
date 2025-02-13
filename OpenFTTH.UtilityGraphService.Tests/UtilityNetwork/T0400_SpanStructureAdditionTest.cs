using DAX.EventProcessing;
using FluentAssertions;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.EventSourcing;
using OpenFTTH.TestData;
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
    [Order(400)]
    public class T0400_SpanStructureAdditionTests
    {
        private readonly IEventStore _eventStore;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly FakeExternalEventProducer _externalEventProducer;

        public T0400_SpanStructureAdditionTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = (FakeExternalEventProducer)externalEventProducer;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public async Task TestAddAdditionalStructuresTo5x10_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipmentId = TestUtilityNetwork.FlexConduit_40_Red_CC_1_to_SP_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var sutSpanEquipment);

            var addStructure = new PlaceAdditionalStructuresInSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                spanEquipmentId: sutSpanEquipmentId,
                structureSpecificationIds: new Guid[] { TestSpecifications.Ø10_Red, TestSpecifications.Ø10_Violet }
            );

            var addStructureResult = await _commandDispatcher.HandleAsync<PlaceAdditionalStructuresInSpanEquipment, Result>(addStructure);

            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
               new GetEquipmentDetails(new EquipmentIdList() { sutSpanEquipmentId })
            );

            var equipmentAfterAddingStructure = equipmentQueryResult.Value.SpanEquipment[sutSpanEquipmentId];

            // Assert
            addStructureResult.IsSuccess.Should().BeTrue();
            equipmentQueryResult.IsSuccess.Should().BeTrue();

            equipmentAfterAddingStructure.SpanStructures.Count(s => s.Level == 2).Should().Be(2);
            equipmentAfterAddingStructure.SpanStructures.Count(s => s.Level == 2 && s.Position == 1).Should().Be(1);
            equipmentAfterAddingStructure.SpanStructures.Count(s => s.Level == 2 && s.Position == 2).Should().Be(1);
            equipmentAfterAddingStructure.SpanStructures[1].Id.Should().NotBeEmpty();
            equipmentAfterAddingStructure.SpanStructures[1].SpanSegments[0].Id.Should().NotBeEmpty();
            equipmentAfterAddingStructure.SpanStructures[2].Id.Should().NotBeEmpty();
            equipmentAfterAddingStructure.SpanStructures[2].SpanSegments[0].Id.Should().NotBeEmpty();

            // Check utility graph
            utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(equipmentAfterAddingStructure.SpanStructures[2].SpanSegments[0].Id, out var fromGraphSegmentRef);
            fromGraphSegmentRef.SpanSegment(utilityNetwork).FromTerminalId.Should().BeEmpty();
            fromGraphSegmentRef.SpanSegment(utilityNetwork).ToTerminalId.Should().BeEmpty();

            
            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification.StructuresAdded" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == sutSpanEquipmentId)));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.CC_1);
        }

        [Fact, Order(2)]
        public async Task TestAddAdditionalStructuresTo5x10_UsingSpanSegmentId_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipmentId = TestUtilityNetwork.FlexConduit_40_Red_CC_1_to_SP_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var sutSpanEquipment);

            var addStructure = new PlaceAdditionalStructuresInSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                spanEquipmentId: sutSpanEquipment.SpanStructures[0].SpanSegments[0].Id,
                structureSpecificationIds: new Guid[] { TestSpecifications.Ø10_Turquoise }
            );

            var addStructureResult = await _commandDispatcher.HandleAsync<PlaceAdditionalStructuresInSpanEquipment, Result>(addStructure);

            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
               new GetEquipmentDetails(new EquipmentIdList() { sutSpanEquipmentId })
            );

            var equipmentAfterAddingStructure = equipmentQueryResult.Value.SpanEquipment[sutSpanEquipmentId];

            // Assert
            addStructureResult.IsSuccess.Should().BeTrue();
            equipmentQueryResult.IsSuccess.Should().BeTrue();

        }

        [Fact, Order(10)]
        public async Task TestAddAdditionalStructuresFixedSpanEquipment_ShouldFail()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipmentId = TestUtilityNetwork.MultiConduit_10x10_HH_1_to_HH_10;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var sutSpanEquipment);

            var addStructure = new PlaceAdditionalStructuresInSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                spanEquipmentId: sutSpanEquipment.SpanStructures[0].SpanSegments[0].Id,
                structureSpecificationIds: new Guid[] { TestSpecifications.Ø10_Turquoise }
            );

            var addStructureResult = await _commandDispatcher.HandleAsync<PlaceAdditionalStructuresInSpanEquipment, Result>(addStructure);

            // Assert
            addStructureResult.IsFailed.Should().BeTrue();

        }
    }
}

#nullable enable
