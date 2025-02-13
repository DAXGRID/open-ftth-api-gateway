using DAX.EventProcessing;
using FluentAssertions;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
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
    [Order(1800)]
    public class T1800_NodeContainerReverseVerticalAlignmentTests
    {
        private readonly IEventStore _eventStore;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly FakeExternalEventProducer _externalEventProducer;

        public T1800_NodeContainerReverseVerticalAlignmentTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = (FakeExternalEventProducer)externalEventProducer;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public async Task ReverseVerticalAlligmentInContainerInJ2_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_J_1;

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerBeforeFlip);

            var reverseCmd = new ReverseNodeContainerVerticalContentAlignment(Guid.NewGuid(), new UserContext("test", Guid.Empty), sutNodeContainerId);
            var reverseResult = await _commandDispatcher.HandleAsync<ReverseNodeContainerVerticalContentAlignment, Result>(reverseCmd);

            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new InterestIdList() { nodeContainerBeforeFlip.InterestId })
            );

            // Assert
            nodeContainerBeforeFlip.VertialContentAlignmemt.Should().Be(NodeContainerVerticalContentAlignmentEnum.Bottom);
            reverseResult.IsSuccess.Should().BeTrue();
            equipmentQueryResult.IsSuccess.Should().BeTrue();
            equipmentQueryResult.Value.NodeContainers[sutNodeContainerId].VertialContentAlignmemt.Should().Be(NodeContainerVerticalContentAlignmentEnum.Top);

            // Check if an event is published to the notification.utility-network topic having an idlist containing the node container we just changed
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == sutNodeContainerId)));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.J_1);
        }


        [Fact, Order(2)]
        public async Task ReverseBackVerticalAlligmentInContainerInJ2_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_J_1;

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerBeforeFlip);

            var reverseCmd = new ReverseNodeContainerVerticalContentAlignment(Guid.NewGuid(), new UserContext("test", Guid.Empty), sutNodeContainerId);
            var reverseResult = await _commandDispatcher.HandleAsync<ReverseNodeContainerVerticalContentAlignment, Result>(reverseCmd);

            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new InterestIdList() { nodeContainerBeforeFlip.InterestId })
            );

            // Assert
            nodeContainerBeforeFlip.VertialContentAlignmemt.Should().Be(NodeContainerVerticalContentAlignmentEnum.Top);
            reverseResult.IsSuccess.Should().BeTrue();
            equipmentQueryResult.IsSuccess.Should().BeTrue();
            equipmentQueryResult.Value.NodeContainers[sutNodeContainerId].VertialContentAlignmemt.Should().Be(NodeContainerVerticalContentAlignmentEnum.Bottom);
        }
    }
}

#nullable enable
