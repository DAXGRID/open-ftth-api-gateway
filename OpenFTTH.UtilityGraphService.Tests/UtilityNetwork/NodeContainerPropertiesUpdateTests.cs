using DAX.EventProcessing;
using FluentAssertions;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.EventSourcing;
using OpenFTTH.TestData;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.NodeContainers.Projections;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions.Ordering;

#nullable disable

namespace OpenFTTH.UtilityGraphService.Tests.UtilityNetwork
{
    [Order(1501)]
    public class T1501_NodeContainerPropertiesUpdateTests
    {
        private readonly IEventStore _eventStore;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly FakeExternalEventProducer _externalEventProducer;

        public T1501_NodeContainerPropertiesUpdateTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = (FakeExternalEventProducer)externalEventProducer;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public async Task UpdateManufacturer_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CC_1;

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerBeforeUpdate);

            var updateCmd = new UpdateNodeContainerProperties(Guid.NewGuid(), new UserContext("test", Guid.Empty), nodeContainerId: sutNodeContainerId)
            {
                ManufacturerId = TestSpecifications.Manu_Fiberpowertech
            };

            var updateResult = await _commandDispatcher.HandleAsync<UpdateNodeContainerProperties, Result>(updateCmd);

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerAfterUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeTrue();
            nodeContainerAfterUpdate.ManufacturerId.Should().Be(updateCmd.ManufacturerId);

            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification.PropertiesUpdated" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == sutNodeContainerId)));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.CC_1);
        }

        [Fact, Order(2)]
        public async Task UpdateManufacturerToGuidEmpty_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CC_1;

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerBeforeUpdate);

            var updateCmd = new UpdateNodeContainerProperties(Guid.NewGuid(), new UserContext("test", Guid.Empty), nodeContainerId: sutNodeContainerId)
            {
                ManufacturerId = Guid.Empty
            };

            var updateResult = await _commandDispatcher.HandleAsync<UpdateNodeContainerProperties, Result>(updateCmd);

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerAfterUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeTrue();
            nodeContainerAfterUpdate.ManufacturerId.Should().Be(updateCmd.ManufacturerId);

            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification.PropertiesUpdated" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == sutNodeContainerId)));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.CC_1);
        }

        [Fact, Order(3)]
        public async Task ChangeSpecificationTo_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
            var nodeContainerSpecifications = _eventStore.Projections.Get<NodeContainerSpecificationsProjection>().Specifications;

            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CC_1;

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerBeforeUpdate);

            var updateCmd = new UpdateNodeContainerProperties(Guid.NewGuid(), new UserContext("test", Guid.Empty), nodeContainerId: sutNodeContainerId)
            {
                SpecificationId = TestSpecifications.Well_Fiberpowertech_37_EK_338_550x1165
            };

            var updateResult = await _commandDispatcher.HandleAsync<UpdateNodeContainerProperties, Result>(updateCmd);

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerAfterUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeTrue();
            nodeContainerAfterUpdate.SpecificationId.Should().Be(updateCmd.SpecificationId.Value);
         
            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification.PropertiesUpdated" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == sutNodeContainerId)));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.CC_1);
        }
    }
}

#nullable enable
