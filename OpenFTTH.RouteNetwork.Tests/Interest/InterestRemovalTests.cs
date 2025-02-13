using FluentAssertions;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.RouteNetwork.Tests.Fixtures;
using System;
using System.Threading.Tasks;
using Xunit;

namespace OpenFTTH.RouteNetworkService.Tests.Interest
{
    public class InterestRemovalTests
    {
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;

        public InterestRemovalTests(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IEventStore eventStore)
        {
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            eventStore.ScanForProjections();
        }

        [Fact]
        public async Task CreateAndRemoveWalkOfInterest_ShouldReturnSuccess()
        {
            // Route network subset used in this test:
            // (CO_1) <- (S1) -> (HH_1)
            var interestId = Guid.NewGuid();

            var walk = new RouteNetworkElementIdList() { TestRouteNetwork.S1 };

            var routeNetworkQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.CO_1 })
            {
                RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
            };

            // Act
            var registerWalkOfInterestCommand = new RegisterWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), interestId, walk);
            var registerWalkOfInterestCommandResult = await _commandDispatcher.HandleAsync<RegisterWalkOfInterest, Result<RouteNetworkInterest>>(registerWalkOfInterestCommand);

            Result<GetRouteNetworkDetailsResult> routeNetworkQueryResultBefore = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkQuery);

            var unregisterWalkOfInterestCommand = new UnregisterInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), interestId);
            var unregisterWalkOfInterestCommandResult = await _commandDispatcher.HandleAsync<UnregisterInterest, Result>(unregisterWalkOfInterestCommand);

            Result<GetRouteNetworkDetailsResult> routeNetworkQueryResultAfter = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkQuery);

            // Assert command result
            registerWalkOfInterestCommandResult.IsSuccess.Should().BeTrue();
            unregisterWalkOfInterestCommandResult.IsSuccess.Should().BeTrue();

            // Assert query result
            routeNetworkQueryResultBefore.IsSuccess.Should().BeTrue();
            routeNetworkQueryResultBefore.Value.Interests.ContainsKey(interestId).Should().BeTrue();

            routeNetworkQueryResultAfter.IsSuccess.Should().BeTrue();
            routeNetworkQueryResultAfter.Value.Interests.ContainsKey(interestId).Should().BeFalse();
        }

        [Fact]
        public async Task CreateAndRemoveNodeOfInterest_ShouldReturnSuccess()
        {
            // Route network subset used in this test:
            // (CO_1) <- (S1) -> (HH_1)
            var interestId = Guid.NewGuid();

            var routeNetworkQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.CO_1 })
            {
                RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
            };

            // Act
            var registerNodeOfInterestCommand = new RegisterNodeOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), interestId, TestRouteNetwork.CO_1);
            var registerNodeOfInterestCommandResult = await _commandDispatcher.HandleAsync<RegisterNodeOfInterest, Result<RouteNetworkInterest>>(registerNodeOfInterestCommand);

            Result<GetRouteNetworkDetailsResult> routeNetworkQueryResultBefore = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkQuery);

            var unregisterWalkOfInterestCommand = new UnregisterInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), interestId);
            var unregisterWalkOfInterestCommandResult = await _commandDispatcher.HandleAsync<UnregisterInterest, Result>(unregisterWalkOfInterestCommand);

            Result<GetRouteNetworkDetailsResult> routeNetworkQueryResultAfter = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkQuery);

            // Assert command result
            registerNodeOfInterestCommandResult.IsSuccess.Should().BeTrue();
            unregisterWalkOfInterestCommandResult.IsSuccess.Should().BeTrue();

            // Assert query result
            routeNetworkQueryResultBefore.IsSuccess.Should().BeTrue();
            routeNetworkQueryResultBefore.Value.Interests.ContainsKey(interestId).Should().BeTrue();

            routeNetworkQueryResultAfter.IsSuccess.Should().BeTrue();
            routeNetworkQueryResultAfter.Value.Interests.ContainsKey(interestId).Should().BeFalse();
        }
    }
}
