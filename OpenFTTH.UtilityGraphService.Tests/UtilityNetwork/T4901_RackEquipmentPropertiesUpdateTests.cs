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
    [Order(4901)]
    public class T4901_RackEquipmentPropertiesUpdateTests
    {
        private readonly IEventStore _eventStore;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly FakeExternalEventProducer _externalEventProducer;

        public T4901_RackEquipmentPropertiesUpdateTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = (FakeExternalEventProducer)externalEventProducer;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public async Task UpdateSpecificationShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.J_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_J_1;

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get equipment
            var rack = nodeContainer.Racks.First();

            var updateCmd = new UpdateRackProperties(Guid.NewGuid(), new UserContext("test", Guid.Empty), sutNodeId, rack.Id)
            {
                SpecificationId = TestSpecifications.Rack_Super
            };

            var updateResult = await _commandDispatcher.HandleAsync<UpdateRackProperties, Result>(updateCmd);

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerAfterUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeTrue();
            nodeContainerAfterUpdate.Racks.First().SpecificationId.Should().Be(updateCmd.SpecificationId.Value);



            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification.PropertiesUpdated" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == (nodeContainer.Id))));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(sutNodeId);
        }

        [Fact, Order(2)]
        public async Task UpdateNameShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.J_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_J_1;

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get equipment
            var rack = nodeContainer.Racks.First();

            var updateCmd = new UpdateRackProperties(Guid.NewGuid(), new UserContext("test", Guid.Empty), sutNodeId, rack.Id)
            {
                Name = "super rack 1"
            };

            var updateResult = await _commandDispatcher.HandleAsync<UpdateRackProperties, Result>(updateCmd);

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerAfterUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeTrue();
            nodeContainerAfterUpdate.Racks.First().Name.Should().Be(updateCmd.Name);
        }

        [Fact, Order(3)]
        public async Task UpdateHeightInUnitsSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.J_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_J_1;

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get equipment
            var rack = nodeContainer.Racks.First();

            var updateCmd = new UpdateRackProperties(Guid.NewGuid(), new UserContext("test", Guid.Empty), sutNodeId, rack.Id)
            {
                HeightInUnits = 100
            };

            var updateResult = await _commandDispatcher.HandleAsync<UpdateRackProperties, Result>(updateCmd);

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerAfterUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeTrue();
            nodeContainerAfterUpdate.Racks.First().HeightInUnits.Should().Be(updateCmd.HeightInUnits.Value);
        }




    }
}

#nullable enable
