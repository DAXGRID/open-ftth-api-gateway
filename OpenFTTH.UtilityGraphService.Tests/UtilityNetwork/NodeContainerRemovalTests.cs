using DAX.EventProcessing;
using FluentAssertions;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.RouteNetwork.API.Model;
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
    [Order(520)]
    public class T0520_NodeContainerRemovalTests
    {
        private readonly IEventStore _eventStore;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly FakeExternalEventProducer _externalEventProducer;

        private static Guid _sutContainerId = Guid.NewGuid();

        public T0520_NodeContainerRemovalTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = (FakeExternalEventProducer)externalEventProducer;
        }

        
        [Fact, Order(1)]
        public async Task TestPlaceNodeContainer_ShouldSucceed()
        {
            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();

            var nodeOfInterestId = Guid.NewGuid();
            var registerNodeOfInterestCommand = new RegisterNodeOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), nodeOfInterestId, TestRouteNetwork.HH_11);
            var registerNodeOfInterestCommandResult = _commandDispatcher.HandleAsync<RegisterNodeOfInterest, Result<RouteNetworkInterest>>(registerNodeOfInterestCommand).Result;

            var placeNodeContainerCommand = new PlaceNodeContainerInRouteNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty), _sutContainerId, TestSpecifications.Conduit_Closure_Emtelle_Branch_Box, registerNodeOfInterestCommandResult.Value)
            {
                ManufacturerId = TestSpecifications.Manu_Emtelle
            };

            // Act
            var placeNodeContainerResult = await _commandDispatcher.HandleAsync<PlaceNodeContainerInRouteNetwork, Result>(placeNodeContainerCommand);

            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new InterestIdList() { nodeOfInterestId })
            );

            // Assert
            placeNodeContainerResult.IsSuccess.Should().BeTrue();
            equipmentQueryResult.IsSuccess.Should().BeTrue();
        }

        [Fact, Order(2)]
        public async Task TestAffixConduitToContainer_ShouldSucceed()
        {
            var testNetwork = new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();

            var testConduitId = TestUtilityNetwork.MultiConduit_3x10_CC_1_to_HH_11;

            var testConduit = _eventStore.Projections.Get<UtilityNetworkProjection>().SpanEquipmentsByEquipmentId[testConduitId];

            var affixConduitToContainerCommand = new AffixSpanEquipmentToNodeContainer(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                spanEquipmentOrSegmentId: testConduit.SpanStructures[0].SpanSegments[0].Id,
                nodeContainerId: _sutContainerId,
                nodeContainerIngoingSide: NodeContainerSideEnum.West
            );

            var affixResult = await _commandDispatcher.HandleAsync<AffixSpanEquipmentToNodeContainer, Result>(affixConduitToContainerCommand);

            affixResult.IsSuccess.Should().BeTrue();
        }


        [Fact, Order(3)]
        public async Task TryRemoveContainer_ShouldFail()
        {
            var removeContainerCommand = new RemoveNodeContainerFromRouteNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                nodeContainerId: _sutContainerId
            );

            var removeResult = await _commandDispatcher.HandleAsync<RemoveNodeContainerFromRouteNetwork, Result>(removeContainerCommand);

            // Assert
            removeResult.IsFailed.Should().BeTrue();

            ((RemoveNodeContainerFromRouteNetworkError)removeResult.Errors.First()).Code.Should().Be(RemoveNodeContainerFromRouteNetworkErrorCodes.CANNOT_REMOVE_NODE_CONTAINER_WITH_AFFIXED_SPAN_EQUIPMENT);

        }

        [Fact, Order(4)]
        public async Task TestDetachConduitToContainer_ShouldSucceed()
        {
            var testConduitId = TestUtilityNetwork.MultiConduit_3x10_CC_1_to_HH_11;

            var testConduit = _eventStore.Projections.Get<UtilityNetworkProjection>().SpanEquipmentsByEquipmentId[testConduitId];

            var nodeContainerId = testConduit.NodeContainerAffixes.First(n => n.RouteNodeId == TestRouteNetwork.HH_11).NodeContainerId;

            var detachConduitFromNodeContainer = new DetachSpanEquipmentFromNodeContainer(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                testConduit.SpanStructures[1].SpanSegments[0].Id,
                routeNodeId: TestRouteNetwork.HH_11
            );

            // Act
            var detachResult = await _commandDispatcher.HandleAsync<DetachSpanEquipmentFromNodeContainer, Result>(detachConduitFromNodeContainer);

            // Assert
            detachResult.IsSuccess.Should().BeTrue();
        }


        [Fact, Order(5)]
        public async Task TryRemoveContainer_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var removeContainerCommand = new RemoveNodeContainerFromRouteNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                nodeContainerId: _sutContainerId
            );

            var removeResult = await _commandDispatcher.HandleAsync<RemoveNodeContainerFromRouteNetwork, Result>(removeContainerCommand);

            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new EquipmentIdList() { _sutContainerId })
            );
            
            // Assert
            removeResult.IsSuccess.Should().BeTrue();

            // Make sure node container is removed from utility network
            utilityNetwork.TryGetEquipment<NodeContainer>(_sutContainerId, out _).Should().BeFalse();

            // Make sure we can't query node container anymore
            equipmentQueryResult.IsSuccess.Should().BeTrue();
            equipmentQueryResult.Value.NodeContainers.Count.Should().Be(0);

            // Check if an event is published to the notification.utility-network topic having an idlist containing the node container we just deleted
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentDeletion" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == _sutContainerId)));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.HH_11);
        }
    }
}

#nullable enable
