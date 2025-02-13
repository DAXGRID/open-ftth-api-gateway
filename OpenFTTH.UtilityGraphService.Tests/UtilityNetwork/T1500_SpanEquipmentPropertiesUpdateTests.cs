using DAX.EventProcessing;
using FluentAssertions;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.TestData;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions.Ordering;

#nullable disable

namespace OpenFTTH.UtilityGraphService.Tests.UtilityNetwork
{
    [Order(1500)]
    public class T1500_SpanEquipmentPropertiesUpdateTests
    {
        private readonly IEventStore _eventStore;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly FakeExternalEventProducer _externalEventProducer;

        public T1500_SpanEquipmentPropertiesUpdateTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = (FakeExternalEventProducer)externalEventProducer;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public async Task UpdateMarkingInfo_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipmentId = TestUtilityNetwork.MultiConduit_3x10_SDU_1_to_SDU_2;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipmentBeforeUpdate);

            var updateCmd = new UpdateSpanEquipmentProperties(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipmentOrSegmentId: sutSpanEquipmentId)
            {
                MarkingInfo = new MarkingInfo() { MarkingColor = "Red", MarkingText = "Rød"}
            };

            var updateResult = await _commandDispatcher.HandleAsync<UpdateSpanEquipmentProperties, Result>(updateCmd);

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipmentAfterUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeTrue();
            spanEquipmentAfterUpdate.MarkingInfo.Should().BeEquivalentTo(updateCmd.MarkingInfo);

            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification.PropertiesUpdated" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == sutSpanEquipmentId)));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.J_1);
        }

        [Fact, Order(2)]
        public async Task UpdateMarkingInfoSetPropertyToNull_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipmentId = TestUtilityNetwork.MultiConduit_3x10_SDU_1_to_SDU_2;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipmentBeforeUpdate);

            var updateCmd = new UpdateSpanEquipmentProperties(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipmentOrSegmentId: spanEquipmentBeforeUpdate.SpanStructures[0].SpanSegments[0].Id)
            {
                MarkingInfo = new MarkingInfo() { MarkingColor = "Red", MarkingText = null }
            };

            var updateResult = await _commandDispatcher.HandleAsync<UpdateSpanEquipmentProperties, Result>(updateCmd);

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipmentAfterUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeTrue();
            spanEquipmentAfterUpdate.MarkingInfo.Should().BeEquivalentTo(updateCmd.MarkingInfo);
        }

        [Fact, Order(3)]
        public async Task UpdateManufacturer_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipmentId = TestUtilityNetwork.MultiConduit_3x10_SDU_1_to_SDU_2;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipmentBeforeUpdate);

            var updateCmd = new UpdateSpanEquipmentProperties(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipmentOrSegmentId: sutSpanEquipmentId)
            {
                ManufacturerId = TestSpecifications.Manu_Emtelle
            };

            var updateResult = await _commandDispatcher.HandleAsync<UpdateSpanEquipmentProperties, Result>(updateCmd);

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipmentAfterUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeTrue();
            spanEquipmentAfterUpdate.ManufacturerId.Should().Be(updateCmd.ManufacturerId);

            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification.PropertiesUpdated" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == sutSpanEquipmentId)));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.J_1);
        }

        [Fact, Order(4)]
        public async Task UpdateManufacturerToGuidEmpty_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipmentId = TestUtilityNetwork.MultiConduit_3x10_SDU_1_to_SDU_2;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipmentBeforeUpdate);

            var updateCmd = new UpdateSpanEquipmentProperties(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipmentOrSegmentId: sutSpanEquipmentId)
            {
                ManufacturerId = Guid.Empty
            };

            var updateResult = await _commandDispatcher.HandleAsync<UpdateSpanEquipmentProperties, Result>(updateCmd);

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipmentAfterUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeTrue();
            spanEquipmentAfterUpdate.ManufacturerId.Should().Be(updateCmd.ManufacturerId);

            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification.PropertiesUpdated" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == sutSpanEquipmentId)));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.J_1);
        }

        [Fact, Order(5)]
        public async Task UpdateAddressInfo_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipmentId = TestUtilityNetwork.MultiConduit_3x10_SDU_1_to_SDU_2;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipmentBeforeUpdate);

            var updateCmd = new UpdateSpanEquipmentProperties(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipmentOrSegmentId: sutSpanEquipmentId)
            {
                AddressInfo = new AddressInfo() { Remark = "Hi", AccessAddressId = Guid.NewGuid(), UnitAddressId = Guid.Empty }
            };

            var updateResult = await _commandDispatcher.HandleAsync<UpdateSpanEquipmentProperties, Result>(updateCmd);

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipmentAfterUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeTrue();
            spanEquipmentAfterUpdate.AddressInfo.Should().BeEquivalentTo(updateCmd.AddressInfo);

            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification.PropertiesUpdated" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == sutSpanEquipmentId)));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.J_1);
        }

        [Fact, Order(6)]
        public async Task UpdateNamingInfo_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipmentId = TestUtilityNetwork.MultiConduit_3x10_SDU_1_to_SDU_2;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipmentBeforeUpdate);

            var updateCmd = new UpdateSpanEquipmentProperties(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipmentOrSegmentId: sutSpanEquipmentId)
            {
                NamingInfo = new NamingInfo() { Name = "Jesper", Description = "Ladegaard" }
            };


            var updateResult = await _commandDispatcher.HandleAsync<UpdateSpanEquipmentProperties, Result>(updateCmd);

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipmentAfterUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeTrue();
            spanEquipmentAfterUpdate.Name.Should().Be("Jesper");
            spanEquipmentAfterUpdate.Description.Should().Be("Ladegaard");
        }

        [Fact, Order(10)]
        public async Task TestCutInnerConduit1In3x10_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipment = TestUtilityNetwork.MultiConduit_3x10_SDU_1_to_SDU_2;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipment, out var spanEquipment);

            // Cut sthe outer conduit and first inner conduit
            var cutCmd = new CutSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.J_1,
                spanSegmentsToCut: new Guid[] {
                    spanEquipment.SpanStructures[0].SpanSegments[0].Id,
                    spanEquipment.SpanStructures[1].SpanSegments[0].Id,
                }
            );

            var cutResult = await _commandDispatcher.HandleAsync<CutSpanSegmentsAtRouteNode, Result>(cutCmd);

            cutResult.IsSuccess.Should().BeTrue();
        }


        [Fact, Order(11)]
        public async Task ChangeSpecificationFrom3x10to12x7_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
            var spanEquipmentSpecifications = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;

            var sutSpanEquipmentId = TestUtilityNetwork.MultiConduit_3x10_SDU_1_to_SDU_2;

            var spanEquipmentSpecification = spanEquipmentSpecifications[TestSpecifications.Multi_Ø40_12x7];

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipmentBeforeUpdate);

            var updateCmd = new UpdateSpanEquipmentProperties(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipmentOrSegmentId: sutSpanEquipmentId)
            {
                SpecificationId = TestSpecifications.Multi_Ø40_12x7
            };

            var updateResult = await _commandDispatcher.HandleAsync<UpdateSpanEquipmentProperties, Result>(updateCmd);

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipmentAfterUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeTrue();
            spanEquipmentAfterUpdate.SpecificationId.Should().Be(updateCmd.SpecificationId.Value);

            spanEquipmentAfterUpdate.SpanStructures.Length.Should().Be(13);
            spanEquipmentAfterUpdate.SpanStructures[0].SpecificationId.Should().Be(spanEquipmentSpecification.RootTemplate.SpanStructureSpecificationId);

            // Check that name of span equipment did not change
            Assert.True(spanEquipmentBeforeUpdate.NamingInfo.Name == spanEquipmentAfterUpdate.NamingInfo.Name);

            // Check that graph contain new segments
            utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(spanEquipmentAfterUpdate.SpanStructures[4].SpanSegments[0].Id, out var _).Should().BeTrue();
            utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(spanEquipmentAfterUpdate.SpanStructures[5].SpanSegments[0].Id, out var _).Should().BeTrue();
            utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(spanEquipmentAfterUpdate.SpanStructures[6].SpanSegments[0].Id, out var _).Should().BeTrue();
            utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(spanEquipmentAfterUpdate.SpanStructures[7].SpanSegments[0].Id, out var _).Should().BeTrue();
            utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(spanEquipmentAfterUpdate.SpanStructures[8].SpanSegments[0].Id, out var _).Should().BeTrue();
            utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(spanEquipmentAfterUpdate.SpanStructures[9].SpanSegments[0].Id, out var _).Should().BeTrue();
            utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(spanEquipmentAfterUpdate.SpanStructures[10].SpanSegments[0].Id, out var _).Should().BeTrue();
            utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(spanEquipmentAfterUpdate.SpanStructures[11].SpanSegments[0].Id, out var _).Should().BeTrue();
            utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(spanEquipmentAfterUpdate.SpanStructures[12].SpanSegments[0].Id, out var _).Should().BeTrue();

            // Check new span segments got correct nodeOfInteresIndexes
            spanEquipmentAfterUpdate.NodesOfInterestIds.Length.Should().Be(3); // 3 because we made a cut in previous test
            spanEquipmentAfterUpdate.SpanStructures[12].SpanSegments[0].FromNodeOfInterestIndex.Should().Be(0);
            spanEquipmentAfterUpdate.SpanStructures[12].SpanSegments[0].ToNodeOfInterestIndex.Should().Be((ushort)(spanEquipmentAfterUpdate.NodesOfInterestIds.Length - 1));

            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification.PropertiesUpdated" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == sutSpanEquipmentId)));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.J_1);
        }

        [Fact, Order(12)]
        public async Task ChangeSpecificationFrom12x7to10x10_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
            var spanEquipmentSpecifications = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;

            var sutSpanEquipmentId = TestUtilityNetwork.MultiConduit_3x10_SDU_1_to_SDU_2;

            var spanEquipmentSpecification = spanEquipmentSpecifications[TestSpecifications.Multi_Ø50_10x10];

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipmentBeforeUpdate);

            var updateCmd = new UpdateSpanEquipmentProperties(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipmentOrSegmentId: sutSpanEquipmentId)
            {
                SpecificationId = TestSpecifications.Multi_Ø50_10x10
            };

            var updateResult = await _commandDispatcher.HandleAsync<UpdateSpanEquipmentProperties, Result>(updateCmd);

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipmentAfterUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeTrue();
            spanEquipmentAfterUpdate.SpecificationId.Should().Be(updateCmd.SpecificationId.Value);

            spanEquipmentAfterUpdate.SpanStructures.Length.Should().Be(11);
            spanEquipmentAfterUpdate.SpanStructures[0].SpecificationId.Should().Be(spanEquipmentSpecification.RootTemplate.SpanStructureSpecificationId);

            // Check that graph contain all 11 span segments
            for (int i = 0; i < 11; i++)
                utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(spanEquipmentAfterUpdate.SpanStructures[i].SpanSegments[0].Id, out var _).Should().BeTrue();

            // Check that graph don't contain old segments
            utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(spanEquipmentBeforeUpdate.SpanStructures[11].SpanSegments[0].Id, out var _).Should().BeFalse();
            utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(spanEquipmentBeforeUpdate.SpanStructures[12].SpanSegments[0].Id, out var _).Should().BeFalse();

            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification.PropertiesUpdated" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == sutSpanEquipmentId)));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.J_1);
        }

        [Fact, Order(100)]
        public async Task UpdateMarkingInfoToSameAsBefore_ShouldFail()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipmentId = TestUtilityNetwork.MultiConduit_3x10_SDU_1_to_SDU_2;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipmentBeforeUpdate);

            var updateCmd = new UpdateSpanEquipmentProperties(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipmentOrSegmentId: sutSpanEquipmentId)
            {
                MarkingInfo = new MarkingInfo() { MarkingColor = "Red", MarkingText = null }
            };

            var updateResult = await _commandDispatcher.HandleAsync<UpdateSpanEquipmentProperties, Result>(updateCmd);

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipmentAfterUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeFalse();
        }
    }
}

#nullable enable
