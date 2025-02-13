using FluentAssertions;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.RouteNetwork.Infos;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.RouteNetwork.Tests.Fixtures;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OpenFTTH.RouteNetwork.Tests
{
    public class RouteNetworksTraceTests : IClassFixture<TestRouteNetwork>
    {
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;

        public RouteNetworksTraceTests(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
        }

        [Fact]
        public async Task QueryHandHoles100MeterRadiusFromJ1_ShouldReturn4HandHandholes()
        {
            // Setup
            var stops = new RouteNodeKindEnum[] { RouteNodeKindEnum.CentralOfficeSmall };
            var interests = new RouteNodeKindEnum[] { RouteNodeKindEnum.HandHole };

            var nearestNodeQuery = new FindNearestRouteNodes(TestRouteNetwork.J_1, 10, 100, stops, interests);

            // Act
            var nearestNodeQueryResult = await _queryDispatcher.HandleAsync<FindNearestRouteNodes, Result<FindNearestRouteNodesResult>>(nearestNodeQuery);

            // Assert
            nearestNodeQueryResult.IsSuccess.Should().BeTrue();

            // We should get 4 hand holes back
            nearestNodeQueryResult.Value.RouteNetworkTraces.Count().Should().Be(4);
            nearestNodeQueryResult.Value.RouteNetworkElements.Count().Should().Be(4);
            nearestNodeQueryResult.Value.RouteNetworkElements.Count(r => r.RouteNodeInfo.Kind == RouteNodeKindEnum.HandHole).Should().Be(4);

            // Check trace for handhole HH_10
            var handHole10Trace = nearestNodeQueryResult.Value.RouteNetworkTraces[TestRouteNetwork.HH_10];
            handHole10Trace.Name.Should().Be("HH-10");
            handHole10Trace.RouteNetworkSegmentIds.Length.Should().Be(3);
            handHole10Trace.RouteNetworkSegmentGeometries.Length.Should().Be(3);
            handHole10Trace.RouteNetworkSegmentIds[0].Should().Be(TestRouteNetwork.S13);
            handHole10Trace.RouteNetworkSegmentIds[1].Should().Be(TestRouteNetwork.S5);
            handHole10Trace.RouteNetworkSegmentIds[2].Should().Be(TestRouteNetwork.S6);
            handHole10Trace.Distance.Should().BeInRange(94, 96);
        }

        [Fact]
        public async Task QueryHandHoles30MeterRadiusFromJ1_ShouldReturn3HandHandholes()
        {
            // Setup
            var stops = new RouteNodeKindEnum[] { RouteNodeKindEnum.CentralOfficeSmall };
            var interests = new RouteNodeKindEnum[] { RouteNodeKindEnum.HandHole };

            var nearestNodeQuery = new FindNearestRouteNodes(TestRouteNetwork.J_1, 10, 30, stops, interests);

            // Act
            var nearestNodeQueryResult = await _queryDispatcher.HandleAsync<FindNearestRouteNodes, Result<FindNearestRouteNodesResult>>(nearestNodeQuery);

            // Assert
            nearestNodeQueryResult.IsSuccess.Should().BeTrue();

            // We should get 4 hand holes back
            nearestNodeQueryResult.Value.RouteNetworkTraces.Count().Should().Be(3);
            nearestNodeQueryResult.Value.RouteNetworkElements.Count().Should().Be(3);
        }
    }
}
