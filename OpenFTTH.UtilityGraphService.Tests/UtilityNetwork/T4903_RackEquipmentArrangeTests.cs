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
    [Order(4903)]
    public class T4903_RackEquipmentArrangeTests
    {
        private readonly IEventStore _eventStore;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly FakeExternalEventProducer _externalEventProducer;

        public T4903_RackEquipmentArrangeTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = (FakeExternalEventProducer)externalEventProducer;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public async Task MoveLGXHolderInDataRackTwoUpShouldSucceed()
        {
            // Setup
            var sutNodeId = TestRouteNetwork.CO_1;

            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);


            // Get data rack
            var dataRack = nodeContainer.Racks.First(r => r.Name == "DATA");

            // Get lgx holder at pos 10)
            var lgx = dataRack.SubrackMounts.First(s => s.Position == 10);


            var moveCmd = new ArrangeRackEquipmentInNodeContainer(
                Guid.NewGuid(),
                new UserContext("test", Guid.Empty),
                sutNodeContainerId,
                lgx.TerminalEquipmentId,
                RackEquipmentArrangeMethodEnum.MoveUp,
                2
            );

            var moveCmdResult = await _commandDispatcher.HandleAsync<ArrangeRackEquipmentInNodeContainer, Result>(moveCmd);

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerAfterUpdate);

            // Assert
            moveCmdResult.IsSuccess.Should().BeTrue();

            // Check if OLT has been moved up 2
            nodeContainerAfterUpdate.Racks.First(r => r.Name == "DATA").SubrackMounts.Any(m => m.TerminalEquipmentId != lgx.TerminalEquipmentId && m.Position == 32).Should().BeTrue();

            // Check if LGX has been moved up 2
            nodeContainerAfterUpdate.Racks.First(r => r.Name == "DATA").SubrackMounts.Any(m => m.TerminalEquipmentId == lgx.TerminalEquipmentId && m.Position == 12).Should().BeTrue();


            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification.RackEquipmentMoved" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == (nodeContainer.Id))));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(sutNodeId);
        }

        [Fact, Order(2)]
        public async Task MoveLGXHolderInDataRackTwoDownShouldSucceed()
        {
            // Setup
            var sutNodeId = TestRouteNetwork.CO_1;

            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);


            // Get data rack
            var dataRack = nodeContainer.Racks.First(r => r.Name == "DATA");

            // Get lgx holder at pos 12)
            var lgx = dataRack.SubrackMounts.First(s => s.Position == 12);


            var moveCmd = new ArrangeRackEquipmentInNodeContainer(
                Guid.NewGuid(),
                new UserContext("test", Guid.Empty),
                sutNodeContainerId,
                lgx.TerminalEquipmentId,
                RackEquipmentArrangeMethodEnum.MoveDown,
                2
            );

            var moveCmdResult = await _commandDispatcher.HandleAsync<ArrangeRackEquipmentInNodeContainer, Result>(moveCmd);

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerAfterUpdate);

            // Assert
            moveCmdResult.IsSuccess.Should().BeTrue();

            // Check if OLT has been moved down 2
            nodeContainerAfterUpdate.Racks.First(r => r.Name == "DATA").SubrackMounts.Any(m => m.TerminalEquipmentId != lgx.TerminalEquipmentId && m.Position == 30).Should().BeTrue();

            // Check if LGX has been moved down 2
            nodeContainerAfterUpdate.Racks.First(r => r.Name == "DATA").SubrackMounts.Any(m => m.TerminalEquipmentId == lgx.TerminalEquipmentId && m.Position == 10).Should().BeTrue();


            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification.RackEquipmentMoved" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == (nodeContainer.Id))));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(sutNodeId);
        }


        [Fact, Order(3)]
        public async Task MoveLGXHolderInDataRackTwoDownShouldFail()
        {
            // Setup
            var sutNodeId = TestRouteNetwork.CO_1;

            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get data rack
            var dataRack = nodeContainer.Racks.First(r => r.Name == "DATA");

            // Get olt on pos 30
            var olt = dataRack.SubrackMounts.First(s => s.Position == 30);


            var moveCmd = new ArrangeRackEquipmentInNodeContainer(
                Guid.NewGuid(),
                new UserContext("test", Guid.Empty),
                sutNodeContainerId,
                olt.TerminalEquipmentId,
                RackEquipmentArrangeMethodEnum.MoveDown,
                17
            );

            var moveCmdResult = await _commandDispatcher.HandleAsync<ArrangeRackEquipmentInNodeContainer, Result>(moveCmd);

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerAfterUpdate);

            // Assert
            moveCmdResult.IsSuccess.Should().BeFalse();
        }

    }
}

#nullable enable
