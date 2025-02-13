using DAX.EventProcessing;
using FluentAssertions;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.TestData;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions.Ordering;

#nullable disable

namespace OpenFTTH.UtilityGraphService.Tests.UtilityNetwork
{
    [Order(550)]
    public class T0550_NodeContainerPlacementTests
    {
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly FakeExternalEventProducer _externalEventProducer;

        public T0550_NodeContainerPlacementTests(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = (FakeExternalEventProducer)externalEventProducer;
        }

        
        [Fact]
        public async Task TestPlaceValidNodeContainer_ShouldSucceed()
        {
            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();

            var nodeOfInterestId = Guid.NewGuid();
            var registerNodeOfInterestCommand = new RegisterNodeOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), nodeOfInterestId, TestRouteNetwork.HH_11);
            var registerNodeOfInterestCommandResult = _commandDispatcher.HandleAsync<RegisterNodeOfInterest, Result<RouteNetworkInterest>>(registerNodeOfInterestCommand).Result;

            var placeNodeContainerCommand = new PlaceNodeContainerInRouteNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty), Guid.NewGuid(), TestSpecifications.Conduit_Closure_Emtelle_Branch_Box, registerNodeOfInterestCommandResult.Value)
            {
                ManufacturerId = TestSpecifications.Manu_Emtelle
            };

            // Act
            var placeNodeContainerResult = await _commandDispatcher.HandleAsync<PlaceNodeContainerInRouteNetwork, Result>(placeNodeContainerCommand);

            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new EquipmentIdList() { placeNodeContainerCommand.NodeContainerId })
            );

            // Assert
            placeNodeContainerResult.IsSuccess.Should().BeTrue();
            equipmentQueryResult.IsSuccess.Should().BeTrue();

            equipmentQueryResult.Value.NodeContainers[placeNodeContainerCommand.NodeContainerId].Id.Should().Be(placeNodeContainerCommand.NodeContainerId);
            equipmentQueryResult.Value.NodeContainers[placeNodeContainerCommand.NodeContainerId].SpecificationId.Should().Be(placeNodeContainerCommand.NodeContainerSpecificationId);
            equipmentQueryResult.Value.NodeContainers[placeNodeContainerCommand.NodeContainerId].ManufacturerId.Should().Be(placeNodeContainerCommand.ManufacturerId);
            equipmentQueryResult.Value.NodeContainers[placeNodeContainerCommand.NodeContainerId].InterestId.Should().Be(nodeOfInterestId);
            equipmentQueryResult.Value.NodeContainers[placeNodeContainerCommand.NodeContainerId].VertialContentAlignmemt.Should().Be(NodeContainerVerticalContentAlignmentEnum.Bottom);

            // Check if an event is published to the notification.utility-network topic having an idlist containing the node container we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == placeNodeContainerCommand.NodeContainerId)));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.HH_11);

        }

        [Fact]
        public async Task TestPlacingMultipleNodeContainerInSameNode_ShouldFail()
        {
            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();

            // First node container
            var registerNodeOfInterestCommand1 = new RegisterNodeOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), Guid.NewGuid(), TestRouteNetwork.FP_2);
            var registerNodeOfInterestCommandResult1 = _commandDispatcher.HandleAsync<RegisterNodeOfInterest, Result<RouteNetworkInterest>>(registerNodeOfInterestCommand1).Result;

            var placeNodeContainerCommand1 = new PlaceNodeContainerInRouteNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty), Guid.NewGuid(), TestSpecifications.Conduit_Closure_Emtelle_Branch_Box, registerNodeOfInterestCommandResult1.Value)
            {
                ManufacturerId = TestSpecifications.Manu_Emtelle
            };

            var firstNodeContainerResult = await _commandDispatcher.HandleAsync<PlaceNodeContainerInRouteNetwork, Result>(placeNodeContainerCommand1);

            // Second node container om same node
            //var registerNodeOfInterestCommand2 = new RegisterNodeOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), Guid.NewGuid(), TestRouteNetwork.FP_2);
            //var registerNodeOfInterestCommandResult2 = _commandDispatcher.HandleAsync<RegisterNodeOfInterest, Result<RouteNetworkInterest>>(registerNodeOfInterestCommand2).Result;

            var placeNodeContainerCommand2 = new PlaceNodeContainerInRouteNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty), Guid.NewGuid(), TestSpecifications.Conduit_Closure_Emtelle_Branch_Box, registerNodeOfInterestCommandResult1.Value)
            {
                ManufacturerId = TestSpecifications.Manu_Emtelle
            };

            var secondNodeContainerResult = await _commandDispatcher.HandleAsync<PlaceNodeContainerInRouteNetwork, Result>(placeNodeContainerCommand2);


            // Assert
            firstNodeContainerResult.IsSuccess.Should().BeTrue();
            secondNodeContainerResult.IsSuccess.Should().BeFalse();

            ((NodeContainerError)secondNodeContainerResult.Errors.First()).Code.Should().Be(NodeContainerErrorCodes.NODE_CONTAINER_ALREADY_EXISTS_IN_ROUTE_NODE);


        }

    }
}

#nullable enable
