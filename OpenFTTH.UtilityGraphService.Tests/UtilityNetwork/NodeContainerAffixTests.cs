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
    [Order(700)]
    public class T0700_NodeContainerAffixTests
    {
        private readonly IEventStore _eventStore;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly FakeExternalEventProducer _externalEventProducer;

        public T0700_NodeContainerAffixTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = (FakeExternalEventProducer)externalEventProducer;
        }
        
        [Fact, Order(10)]
        public async Task TestAffixConduitToContainer_ShouldSucceed()
        {
            var nodeContainerId = PlaceNodeContainer(TestRouteNetwork.HH_2);

            var testNetwork = new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();

            var testConduitId = TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10;

            var testConduit = _eventStore.Projections.Get<UtilityNetworkProjection>().SpanEquipmentsByEquipmentId[testConduitId];

            var affixConduitToContainerCommand = new AffixSpanEquipmentToNodeContainer(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                spanEquipmentOrSegmentId: testConduit.SpanStructures[0].SpanSegments[0].Id,
                nodeContainerId: nodeContainerId,
                nodeContainerIngoingSide: NodeContainerSideEnum.West
            );

            var affixResult = await _commandDispatcher.HandleAsync<AffixSpanEquipmentToNodeContainer, Result>(affixConduitToContainerCommand);

            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
               new GetEquipmentDetails(new EquipmentIdList() { testConduitId })
            );

            equipmentQueryResult.IsSuccess.Should().BeTrue();

            equipmentQueryResult.Value.SpanEquipment[testConduitId].NodeContainerAffixes.First(n => n.NodeContainerId == nodeContainerId).NodeContainerIngoingSide.Should().Be(NodeContainerSideEnum.West);

            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == nodeContainerId) && i.ChangeType == Events.Changes.ChangeTypeEnum.Modification));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.HH_2);

        }

        [Fact, Order(11)]
        public async Task TestDetachConduitToContainer_ShouldSucceed()
        {
            var testConduitId = TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10;

            var testConduit = _eventStore.Projections.Get<UtilityNetworkProjection>().SpanEquipmentsByEquipmentId[testConduitId];

            var nodeContainerId = testConduit.NodeContainerAffixes.First(n => n.RouteNodeId == TestRouteNetwork.HH_2).NodeContainerId;

            var detachConduitFromNodeContainer = new DetachSpanEquipmentFromNodeContainer(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                testConduit.SpanStructures[1].SpanSegments[0].Id,
                routeNodeId: TestRouteNetwork.HH_2
            );

            // Act
            var detachResult = await _commandDispatcher.HandleAsync<DetachSpanEquipmentFromNodeContainer, Result>(detachConduitFromNodeContainer);

            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new EquipmentIdList() { testConduitId })
            );

            // Assert
            detachResult.IsSuccess.Should().BeTrue();
            equipmentQueryResult.IsSuccess.Should().BeTrue();

            var spanEquipmentAfterDetach = equipmentQueryResult.Value.SpanEquipment[testConduitId];

            spanEquipmentAfterDetach.NodeContainerAffixes.Any(n => n.RouteNodeId == TestRouteNetwork.HH_2).Should().BeFalse();

            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            utilityNetworkNotifications.Count(n => n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == nodeContainerId) && i.ChangeType == Events.Changes.ChangeTypeEnum.Modification)).Should().Be(2);

        }

        [Fact, Order(12)]
        public async Task AffixConduitToContainerInHH2_Again_ShouldSucceed()
        {
            var testNetwork = new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();

            var testConduitId = TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10;

            var testConduit = _eventStore.Projections.Get<UtilityNetworkProjection>().SpanEquipmentsByEquipmentId[testConduitId];

            var utilityNetworkNotificationsBeforeAct = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var nodeContainerId = utilityNetworkNotificationsBeforeAct.First(n => n.AffectedRouteNetworkElementIds.Contains(TestRouteNetwork.HH_2) && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.ObjectType == "NodeContainer" && i.ChangeType == Events.Changes.ChangeTypeEnum.Modification)).IdChangeSets.First(c => c.ObjectType == "NodeContainer").IdList[0];

            var affixConduitToContainerCommand = new AffixSpanEquipmentToNodeContainer(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                spanEquipmentOrSegmentId: testConduit.SpanStructures[0].SpanSegments[0].Id,
                nodeContainerId: nodeContainerId,
                nodeContainerIngoingSide: NodeContainerSideEnum.West
            );

            var affixResult = await _commandDispatcher.HandleAsync<AffixSpanEquipmentToNodeContainer, Result>(affixConduitToContainerCommand);

            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
               new GetEquipmentDetails(new EquipmentIdList() { testConduitId })
            );

            equipmentQueryResult.IsSuccess.Should().BeTrue();

            equipmentQueryResult.Value.SpanEquipment[testConduitId].NodeContainerAffixes.First(n => n.NodeContainerId == nodeContainerId).NodeContainerIngoingSide.Should().Be(NodeContainerSideEnum.West);

            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            utilityNetworkNotifications.Count(n => n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == nodeContainerId) && i.ChangeType == Events.Changes.ChangeTypeEnum.Modification)).Should().Be(3);
        }


        [Fact, Order(1000)]
        public async Task TestAffixConduitToContainerTwoTimes_ShouldFaild()
        {
            var nodeContainerId = PlaceNodeContainer(TestRouteNetwork.HH_10);

            var testConduits = new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();

            var testConduitId = TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10;

            var testConduit = _eventStore.Projections.Get<UtilityNetworkProjection>().SpanEquipmentsByEquipmentId[testConduitId];

            var affixConduitToContainerCommand = new AffixSpanEquipmentToNodeContainer(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                spanEquipmentOrSegmentId: testConduit.SpanStructures[0].SpanSegments[0].Id,
                nodeContainerId: nodeContainerId,
                nodeContainerIngoingSide: NodeContainerSideEnum.West
            );

            var affixResult1 = await _commandDispatcher.HandleAsync<AffixSpanEquipmentToNodeContainer, Result>(affixConduitToContainerCommand);
            var affixResult2 = await _commandDispatcher.HandleAsync<AffixSpanEquipmentToNodeContainer, Result>(affixConduitToContainerCommand);


            affixResult1.IsSuccess.Should().BeTrue();
            affixResult2.IsSuccess.Should().BeFalse();
            ((AffixSpanEquipmentToNodeContainerError)affixResult2.Errors.First()).Code.Should().Be(AffixSpanEquipmentToNodeContainerErrorCodes.SPAN_EQUIPMENT_ALREADY_AFFIXED_TO_NODE_CONTAINER);

        }


        private Guid PlaceNodeContainer(Guid routeNodeId)
        {
            var specs = new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();

            var nodeContainerId = Guid.NewGuid();
            var nodeOfInterestId = Guid.NewGuid();
            var registerNodeOfInterestCommand = new RegisterNodeOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), nodeOfInterestId, routeNodeId);
            var registerNodeOfInterestCommandResult = _commandDispatcher.HandleAsync<RegisterNodeOfInterest, Result<RouteNetworkInterest>>(registerNodeOfInterestCommand).Result;

            var placeNodeContainerCommand = new PlaceNodeContainerInRouteNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty), nodeContainerId, TestSpecifications.Conduit_Closure_Emtelle_Branch_Box, registerNodeOfInterestCommandResult.Value)
            {
                ManufacturerId = TestSpecifications.Manu_Emtelle
            };

            var placeNodeContainerResult = _commandDispatcher.HandleAsync<PlaceNodeContainerInRouteNetwork, Result>(placeNodeContainerCommand).Result;

            return nodeContainerId;
        }
    }
}

#nullable enable
