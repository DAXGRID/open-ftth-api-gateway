using DAX.EventProcessing;
using FluentAssertions;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.EventSourcing;
using OpenFTTH.TestData;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions.Ordering;

#nullable disable

namespace OpenFTTH.UtilityGraphService.Tests.UtilityNetwork
{
    [Order(51000)]
    public class T5010_TerminalEquipmentPropertiesUpdateTests
    {
        private readonly IEventStore _eventStore;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly FakeExternalEventProducer _externalEventProducer;

        public T5010_TerminalEquipmentPropertiesUpdateTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
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

            var sutNodeId = TestRouteNetwork.CC_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CC_1;

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get equipment
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.TerminalEquipmentReferences.First(), out var terminalEquipmentBeforeUpdate);

            var updateCmd = new UpdateTerminalEquipmentProperties(Guid.NewGuid(), new UserContext("test", Guid.Empty), terminalEquipmentId: terminalEquipmentBeforeUpdate.Id)
            {
                ManufacturerId = TestSpecifications.Manu_Emtelle
            };

            var updateResult = await _commandDispatcher.HandleAsync<UpdateTerminalEquipmentProperties, Result>(updateCmd);

            utilityNetwork.TryGetEquipment<TerminalEquipment>(terminalEquipmentBeforeUpdate.Id, out var terminalEquipmentAfterUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeTrue();
            terminalEquipmentAfterUpdate.ManufacturerId.Should().Be(updateCmd.ManufacturerId);

            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification.PropertiesUpdated" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == (terminalEquipmentBeforeUpdate.Id))));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(sutNodeId);
        }

        [Fact, Order(2)]
        public async Task UpdateNamingInfo_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CC_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CC_1;

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get equipment
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.TerminalEquipmentReferences.First(), out var terminalEquipmentBeforeUpdate);

            var updateCmd = new UpdateTerminalEquipmentProperties(Guid.NewGuid(), new UserContext("test", Guid.Empty), terminalEquipmentId: terminalEquipmentBeforeUpdate.Id)
            {
                NamingInfo = new NamingInfo() { Name = "Jesper", Description = null }
            };

            var updateResult = await _commandDispatcher.HandleAsync<UpdateTerminalEquipmentProperties, Result>(updateCmd);

            utilityNetwork.TryGetEquipment<TerminalEquipment>(terminalEquipmentBeforeUpdate.Id, out var terminalEquipmentAfterUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeTrue();
            terminalEquipmentAfterUpdate.Name.Should().Be("Jesper");
        }

        [Fact, Order(3)]
        public async Task UpdateAddressInfo_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CC_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CC_1;

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get equipment
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.TerminalEquipmentReferences.First(), out var terminalEquipmentBeforeUpdate);

            var updateCmd = new UpdateTerminalEquipmentProperties(Guid.NewGuid(), new UserContext("test", Guid.Empty), terminalEquipmentId: terminalEquipmentBeforeUpdate.Id)
            {
                AddressInfo = new AddressInfo() { Remark = "Hi", AccessAddressId = Guid.NewGuid(), UnitAddressId = Guid.Empty }
            };

            var updateResult = await _commandDispatcher.HandleAsync<UpdateTerminalEquipmentProperties, Result>(updateCmd);

            utilityNetwork.TryGetEquipment<TerminalEquipment>(terminalEquipmentBeforeUpdate.Id, out var terminalEquipmentAfterUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeTrue();
            terminalEquipmentAfterUpdate.AddressInfo.Should().BeEquivalentTo(updateCmd.AddressInfo);
        }

        [Fact, Order(4)]
        public async Task UpdateInterfaceInfo_SetInfo_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CC_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CC_1;

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get equipment
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.TerminalEquipmentReferences.First(), out var terminalEquipmentBeforeUpdate);

            // Get first terminal structure
            var terminalStructureBeforeUpdate = terminalEquipmentBeforeUpdate.TerminalStructures[0];

            var interfaceInfo = new InterfaceInfo("ge", 1, 1, 1, "mycircuitname");

            var updateCmd = new UpdateTerminalStructureProperties(Guid.NewGuid(), new UserContext("test", Guid.Empty), terminalEquipmentId: terminalEquipmentBeforeUpdate.Id, terminalStructureId: terminalStructureBeforeUpdate.Id)
            {
                StructureSpecificationId = terminalStructureBeforeUpdate.SpecificationId,
                Position = terminalStructureBeforeUpdate.Position,
                InterfaceInfo = interfaceInfo
            };

            var updateResult = await _commandDispatcher.HandleAsync<UpdateTerminalStructureProperties, Result>(updateCmd);

            utilityNetwork.TryGetEquipment<TerminalEquipment>(terminalEquipmentBeforeUpdate.Id, out var terminalEquipmentAfterUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeTrue();
            terminalEquipmentAfterUpdate.TerminalStructures[0].interfaceInfo.Should().BeEquivalentTo(interfaceInfo);
        }

        [Fact, Order(5)]
        public async Task UpdateInterfaceInfo_ModifyInfo_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CC_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CC_1;

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get equipment
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.TerminalEquipmentReferences.First(), out var terminalEquipmentBeforeUpdate);

            // Get first terminal structure
            var terminalStructureBeforeUpdate = terminalEquipmentBeforeUpdate.TerminalStructures[0];

            var interfaceInfo = new InterfaceInfo("ge", 2, 1, 1, "mycircuitname2");

            var updateCmd = new UpdateTerminalStructureProperties(Guid.NewGuid(), new UserContext("test", Guid.Empty), terminalEquipmentId: terminalEquipmentBeforeUpdate.Id, terminalStructureId: terminalStructureBeforeUpdate.Id)
            {
                StructureSpecificationId = terminalStructureBeforeUpdate.SpecificationId,
                Position = terminalStructureBeforeUpdate.Position,
                InterfaceInfo = interfaceInfo
            };

            var updateResult = await _commandDispatcher.HandleAsync<UpdateTerminalStructureProperties, Result>(updateCmd);

            utilityNetwork.TryGetEquipment<TerminalEquipment>(terminalEquipmentBeforeUpdate.Id, out var terminalEquipmentAfterUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeTrue();
            terminalEquipmentAfterUpdate.TerminalStructures[0].interfaceInfo.Should().BeEquivalentTo(interfaceInfo);
        }

        [Fact, Order(6)]
        public async Task UpdateInterfaceInfo_ResetInfo_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CC_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CC_1;

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get equipment
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.TerminalEquipmentReferences.First(), out var terminalEquipmentBeforeUpdate);

            // Get first terminal structure
            var terminalStructureBeforeUpdate = terminalEquipmentBeforeUpdate.TerminalStructures[0];
         
            var updateCmd = new UpdateTerminalStructureProperties(Guid.NewGuid(), new UserContext("test", Guid.Empty), terminalEquipmentId: terminalEquipmentBeforeUpdate.Id, terminalStructureId: terminalStructureBeforeUpdate.Id)
            {
                StructureSpecificationId = terminalStructureBeforeUpdate.SpecificationId,
                Position = terminalStructureBeforeUpdate.Position,
                InterfaceInfo = null
            };

            var updateResult = await _commandDispatcher.HandleAsync<UpdateTerminalStructureProperties, Result>(updateCmd);

            utilityNetwork.TryGetEquipment<TerminalEquipment>(terminalEquipmentBeforeUpdate.Id, out var terminalEquipmentAfterUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeTrue();
            terminalEquipmentAfterUpdate.TerminalStructures[0].interfaceInfo.Should().BeNull();
        }


    }
}

#nullable enable
