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
    public class InterestUpdateTests
    {
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;

        public InterestUpdateTests(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IEventStore eventStore)
        {
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            eventStore.ScanForProjections();
        }

        [Fact]
        public async Task UpdateInterestWithWalidWalk_ShouldReturnSuccess()
        {
            // Route network subset used in this test:
            // (CO_1) <- (S1) -> (HH_1)
            var interestId = Guid.NewGuid();

            var walk = new RouteNetworkElementIdList() { TestRouteNetwork.S1 };

            var updatedWalk = new RouteNetworkElementIdList() { TestRouteNetwork.S2, TestRouteNetwork.S3 };

            var routeNetworkQuery_CO1 = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.CO_1 })
            {
                RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
            };

            var routeNetworkQuery_HH2 = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.HH_2 })
            {
                RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
            };

            // Act
            var registerWalkOfInterestCommand = new RegisterWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), interestId, walk);
            var registerWalkOfInterestCommandResult = await _commandDispatcher.HandleAsync<RegisterWalkOfInterest, Result<RouteNetworkInterest>>(registerWalkOfInterestCommand);

            Result<GetRouteNetworkDetailsResult> routeNetworkQueryResultBefore = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkQuery_CO1);

            var updateWalkOfInterestCommand = new UpdateWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), interestId, updatedWalk);
            var updateWalkOfInterestCommandResult = await _commandDispatcher.HandleAsync<UpdateWalkOfInterest, Result<RouteNetworkInterest>>(updateWalkOfInterestCommand);

            Result<GetRouteNetworkDetailsResult> co1_routeNetworkQueryResultAfter = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkQuery_CO1);
            Result<GetRouteNetworkDetailsResult> hh2_routeNetworkQueryResultAfter = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkQuery_HH2);

            // Assert command result
            registerWalkOfInterestCommandResult.IsSuccess.Should().BeTrue();
            updateWalkOfInterestCommandResult.IsSuccess.Should().BeTrue();
            routeNetworkQueryResultBefore.IsSuccess.Should().BeTrue();
            co1_routeNetworkQueryResultAfter.IsSuccess.Should().BeTrue();
            hh2_routeNetworkQueryResultAfter.IsSuccess.Should().BeTrue();

            // Assert query result
            routeNetworkQueryResultBefore.Value.Interests.ContainsKey(interestId).Should().BeTrue();
            routeNetworkQueryResultBefore.Value.Interests[interestId].RouteNetworkElementRefs.Count.Should().Be(3);

            // Make sure index is updated to include new interest at HH 2
            hh2_routeNetworkQueryResultAfter.Value.Interests.ContainsKey(interestId).Should().BeTrue();
            hh2_routeNetworkQueryResultAfter.Value.Interests[interestId].RouteNetworkElementRefs.Count.Should().Be(5);

            // Make sure index is updated to no longer include interest at CO 1
            co1_routeNetworkQueryResultAfter.Value.Interests.ContainsKey(interestId).Should().BeFalse();
        }

        [Fact]
        public async Task UpdateInterestWithInvalidWalk_ShouldFail()
        {
            // Route network subset used in this test:
            // (CO_1) <- (S1) -> (HH_1)
            var interestId = Guid.NewGuid();

            var walk = new RouteNetworkElementIdList() { TestRouteNetwork.S1 };

            // Walk with hole
            var updatedWalk = new RouteNetworkElementIdList() { TestRouteNetwork.S2, TestRouteNetwork.S5 };

            var routeNetworkQuery_CO1 = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.CO_1 })
            {
                RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
            };

            var routeNetworkQuery_HH2 = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.HH_2 })
            {
                RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
            };

            // Act
            var registerWalkOfInterestCommand = new RegisterWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), interestId, walk);
            var registerWalkOfInterestCommandResult = await _commandDispatcher.HandleAsync<RegisterWalkOfInterest, Result<RouteNetworkInterest>>(registerWalkOfInterestCommand);

            Result<GetRouteNetworkDetailsResult> routeNetworkQueryResultBefore = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkQuery_CO1);

            var updateWalkOfInterestCommand = new UpdateWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), interestId, updatedWalk);
            var updateWalkOfInterestCommandResult = await _commandDispatcher.HandleAsync<UpdateWalkOfInterest, Result<RouteNetworkInterest>>(updateWalkOfInterestCommand);

            Result<GetRouteNetworkDetailsResult> co1_routeNetworkQueryResultAfter = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkQuery_CO1);
            Result<GetRouteNetworkDetailsResult> hh2_routeNetworkQueryResultAfter = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkQuery_HH2);

            // Assert command result
            registerWalkOfInterestCommandResult.IsSuccess.Should().BeTrue();
            updateWalkOfInterestCommandResult.IsFailed.Should().BeTrue();
            routeNetworkQueryResultBefore.IsSuccess.Should().BeTrue();
            co1_routeNetworkQueryResultAfter.IsSuccess.Should().BeTrue();
            hh2_routeNetworkQueryResultAfter.IsSuccess.Should().BeTrue();

            // Assert query result
            routeNetworkQueryResultBefore.Value.Interests.ContainsKey(interestId).Should().BeTrue();
            routeNetworkQueryResultBefore.Value.Interests[interestId].RouteNetworkElementRefs.Count.Should().Be(3);

            // Make sure index is *not* updated to include new interest at HH 2
            hh2_routeNetworkQueryResultAfter.Value.Interests.ContainsKey(interestId).Should().BeFalse();

            // Make sure index is still holding interest at CO 1
            co1_routeNetworkQueryResultAfter.Value.Interests.ContainsKey(interestId).Should().BeTrue();
        }
    }
}
