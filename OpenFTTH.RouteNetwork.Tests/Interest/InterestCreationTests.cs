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

namespace OpenFTTH.RouteNetworkService.Tests.Interest
{
    public class InterestCreationTests
    {
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;

        public InterestCreationTests(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
        }

        [Fact]
        public async Task CreateValidWalkOfInterestUsingOneSegmentIdOnly_ShouldReturnSuccess()
        {
            // Route network subset used in this test:
            // (CO_1) <- (S1) -> (HH_1)
            var interestId = Guid.NewGuid();

            var walk = new RouteNetworkElementIdList() { TestRouteNetwork.S1 };

            // Act
            var registerWalkOfInterestCommand = new RegisterWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), interestId, walk);
            var registerWalkOfInterestCommandResult = await _commandDispatcher.HandleAsync<RegisterWalkOfInterest, Result<RouteNetworkInterest>>(registerWalkOfInterestCommand);

            var routeNetworkQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.CO_1 })
            {
                RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
            };

            Result<GetRouteNetworkDetailsResult> routeNetworkQueryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkQuery);

            // Assert command result
            registerWalkOfInterestCommandResult.IsSuccess.Should().BeTrue();
            registerWalkOfInterestCommandResult.Value.Kind.Should().Be(RouteNetworkInterestKindEnum.WalkOfInterest);
            registerWalkOfInterestCommandResult.Value.RouteNetworkElementRefs.Count.Should().Be(3);
            registerWalkOfInterestCommandResult.Value.RouteNetworkElementRefs.Should().Contain(TestRouteNetwork.CO_1);
            registerWalkOfInterestCommandResult.Value.RouteNetworkElementRefs.Should().Contain(TestRouteNetwork.S1);
            registerWalkOfInterestCommandResult.Value.RouteNetworkElementRefs.Should().Contain(TestRouteNetwork.HH_1);

            // Assert query result
            routeNetworkQueryResult.IsSuccess.Should().BeTrue();
            routeNetworkQueryResult.Value.Interests[interestId].RouteNetworkElementRefs.Count.Should().Be(3);
            routeNetworkQueryResult.Value.Interests[interestId].RouteNetworkElementRefs.Should().Contain(TestRouteNetwork.CO_1);
            routeNetworkQueryResult.Value.Interests[interestId].RouteNetworkElementRefs.Should().Contain(TestRouteNetwork.S1);
            routeNetworkQueryResult.Value.Interests[interestId].RouteNetworkElementRefs.Should().Contain(TestRouteNetwork.HH_1);
            routeNetworkQueryResult.Value.Interests[interestId].RouteNetworkElementRefs.Should().NotContain(TestRouteNetwork.S2);
        }

        [Fact]
        public async Task CreateValidWalkOfInterestUsingThreeSegments_ShouldReturnSuccess()
        {
            // Route network subset used in this test:
            // (CO_1) <- (S1) -> (HH_1) <- (S2) -> (HH_2) <- (S4) -> (CC_1)
            var interestId = Guid.NewGuid();

            var walk = new RouteNetworkElementIdList() { TestRouteNetwork.S1, TestRouteNetwork.S2, TestRouteNetwork.S4 };

            // Act
            var registerWalkOfInterestCommand = new RegisterWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), interestId, walk);
            Result registerWalkOfInterestCommandResult = await _commandDispatcher.HandleAsync<RegisterWalkOfInterest, Result<RouteNetworkInterest>>(registerWalkOfInterestCommand);

            var routeNetworkQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.CO_1 })
            {
                RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
            };

            Result<GetRouteNetworkDetailsResult> routeNetworkQueryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkQuery);

            // Assert
            registerWalkOfInterestCommandResult.IsSuccess.Should().BeTrue();
            routeNetworkQueryResult.IsSuccess.Should().BeTrue();

            routeNetworkQueryResult.Value.Interests[interestId].RouteNetworkElementRefs.Count.Should().Be(7);
            routeNetworkQueryResult.Value.Interests[interestId].RouteNetworkElementRefs.Should().Contain(TestRouteNetwork.CO_1);
            routeNetworkQueryResult.Value.Interests[interestId].RouteNetworkElementRefs.Should().Contain(TestRouteNetwork.S1);
            routeNetworkQueryResult.Value.Interests[interestId].RouteNetworkElementRefs.Should().Contain(TestRouteNetwork.HH_1);
            routeNetworkQueryResult.Value.Interests[interestId].RouteNetworkElementRefs.Should().Contain(TestRouteNetwork.S2);
            routeNetworkQueryResult.Value.Interests[interestId].RouteNetworkElementRefs.Should().Contain(TestRouteNetwork.HH_2);
            routeNetworkQueryResult.Value.Interests[interestId].RouteNetworkElementRefs.Should().Contain(TestRouteNetwork.S4);
            routeNetworkQueryResult.Value.Interests[interestId].RouteNetworkElementRefs.Should().Contain(TestRouteNetwork.CC_1);
        }

        [Fact]
        public async Task CreateValidNodeOfInterest_ShouldReturnSuccess()
        {
            var interestId = Guid.NewGuid();
            var routeNodeId = TestRouteNetwork.CC_1;

            // Act
            var registerNodeOfInterestCommand = new RegisterNodeOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), interestId, routeNodeId);
            var registerNodeOfInterestCommandResult = await _commandDispatcher.HandleAsync<RegisterNodeOfInterest, Result<RouteNetworkInterest>>(registerNodeOfInterestCommand);

            var routeNetworkQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.CC_1 })
            {
                RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
            };

            Result<GetRouteNetworkDetailsResult> routeNetworkQueryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkQuery);

            // Assert command result
            registerNodeOfInterestCommandResult.IsSuccess.Should().BeTrue();
            registerNodeOfInterestCommandResult.Value.Kind.Should().Be(RouteNetworkInterestKindEnum.NodeOfInterest);
            registerNodeOfInterestCommandResult.Value.RouteNetworkElementRefs.Count.Should().Be(1);
            registerNodeOfInterestCommandResult.Value.RouteNetworkElementRefs.Should().Contain(TestRouteNetwork.CC_1);

            // Assert query result
            routeNetworkQueryResult.IsSuccess.Should().BeTrue();
            routeNetworkQueryResult.Value.Interests[interestId].RouteNetworkElementRefs.Count.Should().Be(1);
            routeNetworkQueryResult.Value.Interests[interestId].RouteNetworkElementRefs.Should().Contain(TestRouteNetwork.CC_1);
            routeNetworkQueryResult.Value.RouteNetworkElements[routeNodeId].InterestRelations.Should().Contain(i => i.RefId == interestId && i.RelationKind == RouteNetworkInterestRelationKindEnum.InsideNode);
        }

        [Fact]
        public async Task CreateValidWalkOfWithNonAjacentSegments_ShouldReturnSuccess()
        {
            // Route network subset used in this test:
            // S5-S6-S9-S11
            var interestId = Guid.NewGuid();

            var walk = new RouteNetworkElementIdList() { TestRouteNetwork.S9, TestRouteNetwork.S11, TestRouteNetwork.S6, TestRouteNetwork.S5 };

            // Act
            var registerWalkOfInterestCommand = new RegisterWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), interestId, walk);
            Result registerWalkOfInterestCommandResult = await _commandDispatcher.HandleAsync<RegisterWalkOfInterest, Result<RouteNetworkInterest>>(registerWalkOfInterestCommand);

            var routeNetworkQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.J_2 })
            {
                RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
            };

            Result<GetRouteNetworkDetailsResult> routeNetworkQueryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkQuery);

            // Assert
            registerWalkOfInterestCommandResult.IsSuccess.Should().BeTrue();
            routeNetworkQueryResult.IsSuccess.Should().BeTrue();

            routeNetworkQueryResult.Value.Interests[interestId].RouteNetworkElementRefs.Count.Should().Be(9);
            //routeNetworkQueryResult.Value.Interests[interestId].RouteNetworkElementRefs.Should().Contain(TestRouteNetwork.CO_1);
        }



        [Fact]
        public async Task CreateInvalidWalkOfInterestUsingOneNodeAndOneSegments_ShouldReturnFaliour()
        {
            // Route network subset used in this test:
            // (CO_1) <-> (S1)
            var interestId = Guid.NewGuid();

            var walk = new RouteNetworkElementIdList() { TestRouteNetwork.CO_1, TestRouteNetwork.S1 };

            // Act
            var registerWalkOfInterestCommand = new RegisterWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), interestId, walk);
            Result registerWalkOfInterestCommandResult = await _commandDispatcher.HandleAsync<RegisterWalkOfInterest, Result<RouteNetworkInterest>>(registerWalkOfInterestCommand);

            // Assert
            registerWalkOfInterestCommandResult.IsFailed.Should().BeTrue();
            registerWalkOfInterestCommandResult.Errors.OfType<RegisterWalkOfInterestError>().Should().Contain(e => e.Code == RegisterWalkOfInterestErrorCodes.INVALID_WALK_LAST_ROUTE_NETWORK_ELEMENT_MUST_BE_A_NODE);
        }

        [Fact]
        public async Task CreateInvalidWalkOfInterestUsingTwoSeparatedSegments_ShouldReturnFaliour()
        {
            // Route network subset used in this test:
            // (CO_1) <- (S1) -> (HH_1) hole in the walk here (HH_2) -> (S4) -> (CC_1)
            var interestId = Guid.NewGuid();

            var walk = new RouteNetworkElementIdList() { TestRouteNetwork.S1, TestRouteNetwork.S4 };

            // Act
            var registerWalkOfInterestCommand = new RegisterWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), interestId, walk);
            Result registerWalkOfInterestCommandResult = await _commandDispatcher.HandleAsync<RegisterWalkOfInterest, Result<RouteNetworkInterest>>(registerWalkOfInterestCommand);

            // Assert
            registerWalkOfInterestCommandResult.IsFailed.Should().BeTrue();
            registerWalkOfInterestCommandResult.Errors.OfType<RegisterWalkOfInterestError>().Should().Contain(e => e.Code == RegisterWalkOfInterestErrorCodes.INVALID_WALK_SEGMENTS_ARE_NOT_ADJACENT);
        }

        [Fact]
        public async Task CreateInvalidWalkOfInterestWithNonExistingRouteNetworkElement_ShouldReturnFaliour()
        {
            var interestId = Guid.NewGuid();

            var walk = new RouteNetworkElementIdList() { TestRouteNetwork.S1, TestRouteNetwork.S2, Guid.NewGuid() };

            // Act
            var registerWalkOfInterestCommand = new RegisterWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), interestId, walk);
            Result registerWalkOfInterestCommandResult = await _commandDispatcher.HandleAsync<RegisterWalkOfInterest, Result<RouteNetworkInterest>>(registerWalkOfInterestCommand);

            // Assert
            registerWalkOfInterestCommandResult.IsFailed.Should().BeTrue();
            registerWalkOfInterestCommandResult.Errors.OfType<RegisterWalkOfInterestError>().Should().Contain(e => e.Code == RegisterWalkOfInterestErrorCodes.INVALID_WALK_CANNOT_FIND_ROUTE_NETWORK_ELEMENT);
        }
    }
}
