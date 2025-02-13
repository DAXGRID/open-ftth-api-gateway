using FluentAssertions;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.RouteNetwork.Tests.Fixtures;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OpenFTTH.RouteNetwork.Tests
{
    public class ShortestPathTests : IClassFixture<TestRouteNetwork>
    {
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;

        public ShortestPathTests(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
        }

        [Fact]
        public async Task ShortestPathBetweenHH10AndJ2()
        {
            var shortestPathQuery = new ShortestPathBetweenRouteNodes(TestRouteNetwork.HH_10, TestRouteNetwork.J_2);

            // Act
            var nearestNodeQueryResult = await _queryDispatcher.HandleAsync<ShortestPathBetweenRouteNodes, Result<ShortestPathBetweenRouteNodesResult>>(shortestPathQuery);

            // Assert
            nearestNodeQueryResult.IsSuccess.Should().BeTrue();

            nearestNodeQueryResult.Value.RouteNetworkElementIds.Count().Should().Be(9);

            nearestNodeQueryResult.Value.RouteNetworkElementIds[0].Should().Be(TestRouteNetwork.HH_10);
            nearestNodeQueryResult.Value.RouteNetworkElementIds[1].Should().Be(TestRouteNetwork.S13);
            nearestNodeQueryResult.Value.RouteNetworkElementIds[2].Should().Be(TestRouteNetwork.CC_1);
            nearestNodeQueryResult.Value.RouteNetworkElementIds[3].Should().Be(TestRouteNetwork.S5);
            nearestNodeQueryResult.Value.RouteNetworkElementIds[4].Should().Be(TestRouteNetwork.SP_1);
            nearestNodeQueryResult.Value.RouteNetworkElementIds[5].Should().Be(TestRouteNetwork.S6);
            nearestNodeQueryResult.Value.RouteNetworkElementIds[6].Should().Be(TestRouteNetwork.J_1);
            nearestNodeQueryResult.Value.RouteNetworkElementIds[7].Should().Be(TestRouteNetwork.S9);
            nearestNodeQueryResult.Value.RouteNetworkElementIds[8].Should().Be(TestRouteNetwork.J_2);
        }


        [Fact]
        public async Task ShortestPathBetweenJ1AndJ2()
        {
            var shortestPathQuery = new ShortestPathBetweenRouteNodes(TestRouteNetwork.J_1, TestRouteNetwork.J_2);

            // Act
            var nearestNodeQueryResult = await _queryDispatcher.HandleAsync<ShortestPathBetweenRouteNodes, Result<ShortestPathBetweenRouteNodesResult>>(shortestPathQuery);

            // Assert
            nearestNodeQueryResult.IsSuccess.Should().BeTrue();

            nearestNodeQueryResult.Value.RouteNetworkElementIds.Count().Should().Be(3);

            nearestNodeQueryResult.Value.RouteNetworkElementIds[0].Should().Be(TestRouteNetwork.J_1);
            nearestNodeQueryResult.Value.RouteNetworkElementIds[1].Should().Be(TestRouteNetwork.S9);
            nearestNodeQueryResult.Value.RouteNetworkElementIds[2].Should().Be(TestRouteNetwork.J_2);
        }

        [Fact]
        public async Task ShortestPathBetweenJ2AndJ1()
        {
            var shortestPathQuery = new ShortestPathBetweenRouteNodes(TestRouteNetwork.J_2, TestRouteNetwork.J_1);

            // Act
            var nearestNodeQueryResult = await _queryDispatcher.HandleAsync<ShortestPathBetweenRouteNodes, Result<ShortestPathBetweenRouteNodesResult>>(shortestPathQuery);

            // Assert
            nearestNodeQueryResult.IsSuccess.Should().BeTrue();

            nearestNodeQueryResult.Value.RouteNetworkElementIds.Count().Should().Be(3);

            nearestNodeQueryResult.Value.RouteNetworkElementIds[0].Should().Be(TestRouteNetwork.J_2);
            nearestNodeQueryResult.Value.RouteNetworkElementIds[1].Should().Be(TestRouteNetwork.S9);
            nearestNodeQueryResult.Value.RouteNetworkElementIds[2].Should().Be(TestRouteNetwork.J_1);
        }

        [Fact]
        public async Task ShortestPathBetweenJ1AndSDU3()
        {
            var shortestPathQuery = new ShortestPathBetweenRouteNodes(TestRouteNetwork.J_1, TestRouteNetwork.SDU_3);

            // Act
            var nearestNodeQueryResult = await _queryDispatcher.HandleAsync<ShortestPathBetweenRouteNodes, Result<ShortestPathBetweenRouteNodesResult>>(shortestPathQuery);

            // Assert
            nearestNodeQueryResult.IsSuccess.Should().BeTrue();

            nearestNodeQueryResult.Value.RouteNetworkElementIds.Count().Should().Be(5);

            nearestNodeQueryResult.Value.RouteNetworkElementIds[0].Should().Be(TestRouteNetwork.J_1);
            nearestNodeQueryResult.Value.RouteNetworkElementIds[1].Should().Be(TestRouteNetwork.S9);
            nearestNodeQueryResult.Value.RouteNetworkElementIds[2].Should().Be(TestRouteNetwork.J_2);
            nearestNodeQueryResult.Value.RouteNetworkElementIds[3].Should().Be(TestRouteNetwork.S10);
            nearestNodeQueryResult.Value.RouteNetworkElementIds[4].Should().Be(TestRouteNetwork.SDU_3);
        }


        [Fact]
        public async Task ShortestPathBetweenSDU3AndJ1()
        {
            var shortestPathQuery = new ShortestPathBetweenRouteNodes(TestRouteNetwork.SDU_3, TestRouteNetwork.J_1);

            // Act
            var nearestNodeQueryResult = await _queryDispatcher.HandleAsync<ShortestPathBetweenRouteNodes, Result<ShortestPathBetweenRouteNodesResult>>(shortestPathQuery);

            // Assert
            nearestNodeQueryResult.IsSuccess.Should().BeTrue();

            nearestNodeQueryResult.Value.RouteNetworkElementIds.Count().Should().Be(5);

            nearestNodeQueryResult.Value.RouteNetworkElementIds[4].Should().Be(TestRouteNetwork.J_1);
            nearestNodeQueryResult.Value.RouteNetworkElementIds[3].Should().Be(TestRouteNetwork.S9);
            nearestNodeQueryResult.Value.RouteNetworkElementIds[2].Should().Be(TestRouteNetwork.J_2);
            nearestNodeQueryResult.Value.RouteNetworkElementIds[1].Should().Be(TestRouteNetwork.S10);
            nearestNodeQueryResult.Value.RouteNetworkElementIds[0].Should().Be(TestRouteNetwork.SDU_3);

            RouteNetworkElementIdList list = new();
            list.AddRange(nearestNodeQueryResult.Value.RouteNetworkElementIds);

            var validatorQuery = new ValidateWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), list);

            var validatoresult = await _commandDispatcher.HandleAsync<ValidateWalkOfInterest, Result<ValidatedRouteNetworkWalk>>(validatorQuery);
            validatoresult.IsSuccess.Should().BeTrue();
            

        }

        [Fact]
        public async Task ShortestPathBetweenJ1AndJ1()
        {
            var shortestPathQuery = new ShortestPathBetweenRouteNodes(TestRouteNetwork.J_1, TestRouteNetwork.J_1);

            // Act
            var nearestNodeQueryResult = await _queryDispatcher.HandleAsync<ShortestPathBetweenRouteNodes, Result<ShortestPathBetweenRouteNodesResult>>(shortestPathQuery);

            // Assert
            nearestNodeQueryResult.IsSuccess.Should().BeFalse();
        }
    }
}
