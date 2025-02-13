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
    [Order(4902)]
    public class T4902_RackEquipmentMoveTests
    {
        private readonly IEventStore _eventStore;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly FakeExternalEventProducer _externalEventProducer;

        public T4902_RackEquipmentMoveTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = (FakeExternalEventProducer)externalEventProducer;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public async Task MoveOLTRackEquipmentFromDATAToODFRackShouldSucceed()
        {
            // Setup
            var sutNodeId = TestRouteNetwork.CO_1;

            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);


            // Get data rack
            var dataRack = nodeContainer.Racks.First(r => r.Name == "DATA");

            // Get old in data rack (we know it sits on 30 according to previous placement test)
            var olt = dataRack.SubrackMounts.First(s => s.Position == 30);


            // Get odf rack
            var odfRack = nodeContainer.Racks.First(r => r.Name == "ODF");


            var moveCmd = new MoveRackEquipmentInNodeContainer(
                Guid.NewGuid(),
                new UserContext("test", Guid.Empty),
                sutNodeContainerId,
                olt.TerminalEquipmentId,
                odfRack.Id,
                90
            );

            var moveCmdResult = await _commandDispatcher.HandleAsync<MoveRackEquipmentInNodeContainer, Result>(moveCmd);

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerAfterUpdate);

            // Assert
            moveCmdResult.IsSuccess.Should().BeTrue();

            // Check if OLT has been moved from DATA to ODF rack
            nodeContainerAfterUpdate.Racks.First(r => r.Name == "DATA").SubrackMounts.Any(m => m.TerminalEquipmentId == olt.TerminalEquipmentId).Should().BeFalse();
            nodeContainerAfterUpdate.Racks.First(r => r.Name == "ODF").SubrackMounts.Any(m => m.TerminalEquipmentId == olt.TerminalEquipmentId).Should().BeTrue();

            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification.RackEquipmentMoved" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == (nodeContainer.Id))));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(sutNodeId);
        }

        [Fact, Order(2)]
        public async Task MoveOLTRackBackShouldSucceed()
        {
            // Setup
            var sutNodeId = TestRouteNetwork.CO_1;

            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);


            // Get data rack
            var dataRack = nodeContainer.Racks.First(r => r.Name == "DATA");


            // Get odf rack
            var odfRack = nodeContainer.Racks.First(r => r.Name == "ODF");

            // Get old in odf rack (we know it sits on 90 according to previous movement test)
            var olt = odfRack.SubrackMounts.First(s => s.Position == 90);


            var moveCmd = new MoveRackEquipmentInNodeContainer(
                Guid.NewGuid(),
                new UserContext("test", Guid.Empty),
                sutNodeContainerId,
                olt.TerminalEquipmentId,
                dataRack.Id,
                30
            );

            var moveCmdResult = await _commandDispatcher.HandleAsync<MoveRackEquipmentInNodeContainer, Result>(moveCmd);

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerAfterUpdate);

            // Assert
            moveCmdResult.IsSuccess.Should().BeTrue();

            // Check if OLT has been moved back to DATA rack
            nodeContainerAfterUpdate.Racks.First(r => r.Name == "ODF").SubrackMounts.Any(m => m.TerminalEquipmentId == olt.TerminalEquipmentId).Should().BeFalse();
            nodeContainerAfterUpdate.Racks.First(r => r.Name == "DATA").SubrackMounts.Any(m => m.TerminalEquipmentId == olt.TerminalEquipmentId).Should().BeTrue();

            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification.RackEquipmentMoved" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == (nodeContainer.Id))));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(sutNodeId);
        }

        [Fact, Order(3)]
        public async Task MoveOLTRackDownOnePositionShouldSucceed()
        {
            // Setup
            var sutNodeId = TestRouteNetwork.CO_1;

            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);


            // Get data rack
            var dataRack = nodeContainer.Racks.First(r => r.Name == "DATA");
            var olt = dataRack.SubrackMounts.First(s => s.Position == 30);


            var moveCmd = new MoveRackEquipmentInNodeContainer(
                Guid.NewGuid(),
                new UserContext("test", Guid.Empty),
                sutNodeContainerId,
                olt.TerminalEquipmentId,
                dataRack.Id,
                29
            );

            var moveCmdResult = await _commandDispatcher.HandleAsync<MoveRackEquipmentInNodeContainer, Result>(moveCmd);

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerAfterUpdate);

            // Assert
            moveCmdResult.IsSuccess.Should().BeTrue();

            // Check that OLT is still sitting at pos 29
            nodeContainerAfterUpdate.Racks.First(r => r.Name == "DATA").SubrackMounts.Any(m => m.TerminalEquipmentId == olt.TerminalEquipmentId && m.Position == 29).Should().BeTrue();
        }

        [Fact, Order(4)]
        public async Task MoveOLTRackDownToPosition14ShouldSucceed()
        {
            // Setup
            var sutNodeId = TestRouteNetwork.CO_1;

            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);


            // Get data rack
            var dataRack = nodeContainer.Racks.First(r => r.Name == "DATA");
            var olt = dataRack.SubrackMounts.First(s => s.Position == 29);


            var moveCmd = new MoveRackEquipmentInNodeContainer(
                Guid.NewGuid(),
                new UserContext("test", Guid.Empty),
                sutNodeContainerId,
                olt.TerminalEquipmentId,
                dataRack.Id,
                14
            );

            var moveCmdResult = await _commandDispatcher.HandleAsync<MoveRackEquipmentInNodeContainer, Result>(moveCmd);

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerAfterUpdate);

            // Assert
            moveCmdResult.IsSuccess.Should().BeTrue();

            // Check if OLT has been moved back to DATA rack
            nodeContainerAfterUpdate.Racks.First(r => r.Name == "DATA").SubrackMounts.Any(m => m.TerminalEquipmentId == olt.TerminalEquipmentId && m.Position == 14).Should().BeTrue();

            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification.RackEquipmentMoved" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == (nodeContainer.Id))));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(sutNodeId);
        }

      

        [Fact, Order(5)]
        public async Task MoveOLTRackDownToPosition13ShouldFail()
        {
            // Setup
            var sutNodeId = TestRouteNetwork.CO_1;

            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);


            // Get data rack
            var dataRack = nodeContainer.Racks.First(r => r.Name == "DATA");
            var olt = dataRack.SubrackMounts.First(s => s.Position == 14);


            var moveCmd = new MoveRackEquipmentInNodeContainer(
                Guid.NewGuid(),
                new UserContext("test", Guid.Empty),
                sutNodeContainerId,
                olt.TerminalEquipmentId,
                dataRack.Id,
                13
            );

            var moveCmdResult = await _commandDispatcher.HandleAsync<MoveRackEquipmentInNodeContainer, Result>(moveCmd);

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerAfterUpdate);

            // Assert
            moveCmdResult.IsFailed.Should().BeTrue();

            // Check that OLT is still sitting at pos 14
            nodeContainerAfterUpdate.Racks.First(r => r.Name == "DATA").SubrackMounts.Any(m => m.TerminalEquipmentId == olt.TerminalEquipmentId && olt.Position == 14).Should().BeTrue();
        }

       

        [Fact, Order(6)]
        public async Task MoveOLTBackToPosition30ShouldSucceed()
        {
            // Setup
            var sutNodeId = TestRouteNetwork.CO_1;

            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);


            // Get data rack
            var dataRack = nodeContainer.Racks.First(r => r.Name == "DATA");
            var olt = dataRack.SubrackMounts.First(s => s.Position == 14);


            var moveCmd = new MoveRackEquipmentInNodeContainer(
                Guid.NewGuid(),
                new UserContext("test", Guid.Empty),
                sutNodeContainerId,
                olt.TerminalEquipmentId,
                dataRack.Id,
                30
            );

            var moveCmdResult = await _commandDispatcher.HandleAsync<MoveRackEquipmentInNodeContainer, Result>(moveCmd);

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerAfterUpdate);

            // Assert
            moveCmdResult.IsSuccess.Should().BeTrue();

            // Check if OLT has been moved back to DATA rack
            nodeContainerAfterUpdate.Racks.First(r => r.Name == "DATA").SubrackMounts.Any(m => m.TerminalEquipmentId == olt.TerminalEquipmentId && m.Position == 30).Should().BeTrue();

            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification.RackEquipmentMoved" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == (nodeContainer.Id))));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(sutNodeId);
        }

    }
}

#nullable enable
