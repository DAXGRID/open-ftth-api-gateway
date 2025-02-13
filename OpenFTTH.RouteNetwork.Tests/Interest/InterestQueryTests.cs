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
    public class InterestQueryTests : IClassFixture<TestRouteNetwork>
    {
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;

        public InterestQueryTests(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
        }

        [Fact]
        public async Task QueryReferencesFromRouteElementAndInterestObjects_ShouldReturnAllInterestInformation()
        {
            // Create interest (CO_1) <- (S1) -> (HH_1) that we can then try query
            var interestId = Guid.NewGuid();

            var walk = new RouteNetworkElementIdList() { TestRouteNetwork.S1 };
            var createInterestCommand = new RegisterWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), interestId, walk);

            await _commandDispatcher.HandleAsync<RegisterWalkOfInterest, Result<RouteNetworkInterest>>(createInterestCommand);

            // Act: Query CO_1, S1, HH_1 and HH_2
            var routeNetworkQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.CO_1, TestRouteNetwork.S1, TestRouteNetwork.HH_1, TestRouteNetwork.HH_2 })
            {
                RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
            };

            Result<GetRouteNetworkDetailsResult> queryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkQuery);

            // Assert that we got information back on all 4 network elements queried
            Assert.True(queryResult.IsSuccess);
            Assert.Equal(4, queryResult.Value.RouteNetworkElements.Count);

            // Assert that we got back one interest related to the 3 network elements that the walk of interest covers
            Assert.NotNull(queryResult.Value.Interests);
            Assert.Contains(queryResult.Value.Interests, i => i.Id == interestId);
            Assert.Equal(RouteNetworkInterestKindEnum.WalkOfInterest, queryResult.Value.Interests[interestId].Kind);
            Assert.Equal(3, queryResult.Value.Interests[interestId].RouteNetworkElementRefs.Count);
            Assert.Contains(TestRouteNetwork.CO_1, queryResult.Value.Interests[interestId].RouteNetworkElementRefs);
            Assert.Contains(TestRouteNetwork.S1, queryResult.Value.Interests[interestId].RouteNetworkElementRefs);
            Assert.Contains(TestRouteNetwork.HH_1, queryResult.Value.Interests[interestId].RouteNetworkElementRefs);

            // Assert that route element 1 (CO_1) has interest information with correct relation type
            Assert.NotNull(queryResult.Value.RouteNetworkElements[TestRouteNetwork.CO_1].InterestRelations);
            Assert.Contains(queryResult.Value.RouteNetworkElements[TestRouteNetwork.CO_1].InterestRelations, r => r.RefId == interestId && r.RelationKind == RouteNetworkInterestRelationKindEnum.Start);

            // Assert that route element 2 (S1) has interest information with correct relation type
            Assert.NotNull(queryResult.Value.RouteNetworkElements[TestRouteNetwork.S1].InterestRelations);
            Assert.Contains(queryResult.Value.RouteNetworkElements[TestRouteNetwork.S1].InterestRelations, r => r.RefId == interestId && r.RelationKind == RouteNetworkInterestRelationKindEnum.PassThrough);

            // Assert that route element 3 (HH_1) has interest information with correct relation type
            Assert.NotNull(queryResult.Value.RouteNetworkElements[TestRouteNetwork.HH_1].InterestRelations);
            Assert.Contains(queryResult.Value.RouteNetworkElements[TestRouteNetwork.HH_1].InterestRelations, r => r.RefId == interestId && r.RelationKind == RouteNetworkInterestRelationKindEnum.End);
        }

        [Fact]
        public async Task QueryReferencesFromRouteElementOnly_ShouldReturnInterestReferencesOnly()
        {
            // Create interest (CO_1) <- (S1) -> (HH_1) that we can then try query
            var interestId = Guid.NewGuid();

            var walk = new RouteNetworkElementIdList() { TestRouteNetwork.S1 };
            var createInterestCommand = new RegisterWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), interestId, walk);

            await _commandDispatcher.HandleAsync<RegisterWalkOfInterest, Result<RouteNetworkInterest>>(createInterestCommand);

            // Act: Query CO_1, S1, HH_1 and HH_2
            var routeNetworkQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.CO_1, TestRouteNetwork.S1, TestRouteNetwork.HH_1, TestRouteNetwork.HH_2 })
            {
                RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementOnly
            };

            Result<GetRouteNetworkDetailsResult> queryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkQuery);

            // Assert that we got information back on all 4 network elements queried
            Assert.True(queryResult.IsSuccess);
            Assert.Equal(4, queryResult.Value.RouteNetworkElements.Count);

            // Assert that we did'nt get any interest object back
            Assert.Empty(queryResult.Value.Interests);

            // Assert that route element 1 (CO_1) has interest information with correct relation type
            Assert.NotNull(queryResult.Value.RouteNetworkElements[TestRouteNetwork.CO_1].InterestRelations);
            Assert.Contains(queryResult.Value.RouteNetworkElements[TestRouteNetwork.CO_1].InterestRelations, r => r.RefId == interestId && r.RelationKind == RouteNetworkInterestRelationKindEnum.Start);

            // Assert that route element 2 (S1) has interest information with correct relation type
            Assert.NotNull(queryResult.Value.RouteNetworkElements[TestRouteNetwork.S1].InterestRelations);
            Assert.Contains(queryResult.Value.RouteNetworkElements[TestRouteNetwork.S1].InterestRelations, r => r.RefId == interestId && r.RelationKind == RouteNetworkInterestRelationKindEnum.PassThrough);

            // Assert that route element 3 (HH_1) has interest information with correct relation type
            Assert.NotNull(queryResult.Value.RouteNetworkElements[TestRouteNetwork.HH_1].InterestRelations);
            Assert.Contains(queryResult.Value.RouteNetworkElements[TestRouteNetwork.HH_1].InterestRelations, r => r.RefId == interestId && r.RelationKind == RouteNetworkInterestRelationKindEnum.End);
        }

        [Fact]
        public async Task QueryExplicitlyRequestingNoInterestInformation_ShouldReturnNoInterestInformation()
        {
            // Create interest (CO_1) <- (S1) -> (HH_1) that we can then try query
            var interestId = Guid.NewGuid();

            var walk = new RouteNetworkElementIdList() { TestRouteNetwork.S1 };
            var createInterestCommand = new RegisterWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), interestId, walk);

            await _commandDispatcher.HandleAsync<RegisterWalkOfInterest, Result<RouteNetworkInterest>>(createInterestCommand);

            // Act: Query CO_1, S1, HH_1 and HH_2
            var routeNetworkQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.CO_1, TestRouteNetwork.S1, TestRouteNetwork.HH_1, TestRouteNetwork.HH_2 })
            {
                RelatedInterestFilter = RelatedInterestFilterOptions.None
            };

            Result<GetRouteNetworkDetailsResult> queryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkQuery);

            // Assert that we got information back on all 4 network elements queried
            Assert.True(queryResult.IsSuccess);
            Assert.Equal(4, queryResult.Value.RouteNetworkElements.Count);

            // Assert that we did'nt get any interest object back
            Assert.Empty(queryResult.Value.Interests);

            // Assert that no route elements got interest relations
            Assert.Null(queryResult.Value.RouteNetworkElements[TestRouteNetwork.CO_1].InterestRelations);
            Assert.Null(queryResult.Value.RouteNetworkElements[TestRouteNetwork.S1].InterestRelations);
            Assert.Null(queryResult.Value.RouteNetworkElements[TestRouteNetwork.HH_1].InterestRelations);
            Assert.Null(queryResult.Value.RouteNetworkElements[TestRouteNetwork.HH_2].InterestRelations);
        }

        [Fact]
        public async Task QueryByInterestId_ShouldSucced()
        {
            // Create two overlapping walk of interests that we can try to query on
            var interest1Id = Guid.NewGuid();
            var walk1 = new RouteNetworkElementIdList() { TestRouteNetwork.S1 };
            await _commandDispatcher.HandleAsync<RegisterWalkOfInterest, Result<RouteNetworkInterest>>(new RegisterWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), interest1Id, walk1));

            var interest2Id = Guid.NewGuid();
            var walk2 = new RouteNetworkElementIdList() { TestRouteNetwork.S2 };
            await _commandDispatcher.HandleAsync<RegisterWalkOfInterest, Result<RouteNetworkInterest>>(new RegisterWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), interest2Id, walk2));


            // Act
            var routeNetworkQuery = new GetRouteNetworkDetails(new InterestIdList() { interest1Id, interest2Id })
            {
                RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
            };

            Result<GetRouteNetworkDetailsResult> queryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkQuery);

            // Assert
            queryResult.Value.Interests.ContainsKey(interest1Id).Should().BeTrue();
            queryResult.Value.Interests.ContainsKey(interest2Id).Should().BeTrue();

            queryResult.Value.RouteNetworkElements.ContainsKey(TestRouteNetwork.CO_1).Should().BeTrue();
            queryResult.Value.RouteNetworkElements.ContainsKey(TestRouteNetwork.S1).Should().BeTrue();
            queryResult.Value.RouteNetworkElements.ContainsKey(TestRouteNetwork.HH_1).Should().BeTrue();
            queryResult.Value.RouteNetworkElements.ContainsKey(TestRouteNetwork.S2).Should().BeTrue();
            queryResult.Value.RouteNetworkElements.ContainsKey(TestRouteNetwork.HH_2).Should().BeTrue();

            queryResult.Value.RouteNetworkElements[TestRouteNetwork.CO_1].InterestRelations.Should().Contain(i => i.RefId == interest1Id && i.RelationKind == RouteNetworkInterestRelationKindEnum.Start);
            queryResult.Value.RouteNetworkElements[TestRouteNetwork.CO_1].InterestRelations.Should().NotContain(i => i.RefId == interest2Id);

            queryResult.Value.RouteNetworkElements[TestRouteNetwork.S1].InterestRelations.Should().Contain(i => i.RefId == interest1Id && i.RelationKind == RouteNetworkInterestRelationKindEnum.PassThrough);
            queryResult.Value.RouteNetworkElements[TestRouteNetwork.S1].InterestRelations.Should().NotContain(i => i.RefId == interest2Id);

            queryResult.Value.RouteNetworkElements[TestRouteNetwork.HH_1].InterestRelations.Should().Contain(i => i.RefId == interest1Id && i.RelationKind == RouteNetworkInterestRelationKindEnum.End);
            queryResult.Value.RouteNetworkElements[TestRouteNetwork.HH_1].InterestRelations.Should().Contain(i => i.RefId == interest2Id && i.RelationKind == RouteNetworkInterestRelationKindEnum.Start);

            queryResult.Value.RouteNetworkElements[TestRouteNetwork.S2].InterestRelations.Should().Contain(i => i.RefId == interest2Id && i.RelationKind == RouteNetworkInterestRelationKindEnum.PassThrough);
            queryResult.Value.RouteNetworkElements[TestRouteNetwork.S2].InterestRelations.Should().NotContain(i => i.RefId == interest1Id);

            queryResult.Value.RouteNetworkElements[TestRouteNetwork.HH_2].InterestRelations.Should().Contain(i => i.RefId == interest2Id && i.RelationKind == RouteNetworkInterestRelationKindEnum.End);
            queryResult.Value.RouteNetworkElements[TestRouteNetwork.HH_2].InterestRelations.Should().NotContain(i => i.RefId == interest1Id);
        }

        [Fact]
        public async Task QueryByNonExistingInterestId_ShouldFail()
        {
            // Act
            var routeNetworkQuery = new GetRouteNetworkDetails(new InterestIdList() { Guid.NewGuid() })
            {
                RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
            };

            Result<GetRouteNetworkDetailsResult> queryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkQuery);

            // Assert
            queryResult.IsFailed.Should().BeTrue();
            queryResult.Errors.OfType<GetRouteNetworkDetailsError>().Should().Contain(e => e.Code == GetRouteNetworkDetailsErrorCodes.INVALID_QUERY_ARGUMENT_ERROR_LOOKING_UP_SPECIFIED_INTEREST_BY_ID);
        }
    }
}
