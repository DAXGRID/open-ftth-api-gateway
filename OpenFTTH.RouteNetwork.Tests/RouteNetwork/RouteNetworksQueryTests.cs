using FluentAssertions;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.Events.RouteNetwork.Infos;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.RouteNetwork.Tests.Fixtures;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OpenFTTH.RouteNetwork.Tests
{
    public class RouteNetworksQueryTests : IClassFixture<TestRouteNetwork>
    {
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;

        public RouteNetworksQueryTests(
            ICommandDispatcher commandDispatcher,
            IQueryDispatcher queryDispatcher,
            IEventStore eventStore)
        {
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            eventStore.ScanForProjections();
        }

        [Fact]
        public async Task QueryRouteElement_ThatDontExists_ShouldReturnFailure()
        {
            // Setup
            var nonExistingRouteNetworkElementId = Guid.NewGuid();

            var routeNodeQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { nonExistingRouteNetworkElementId });

            // Act
            var routeNodeQueryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNodeQuery);

            // Assert
            Assert.True(routeNodeQueryResult.IsFailed);
        }

        [Fact]
        public async Task QueryRouteElement_ThatExists_ShouldReturnSuccessAndAllRouteElementProperties()
        {
            // Setup
            var routeNodeQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.CO_1 });

            // Act
            var routeNodeQueryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNodeQuery);

            // Assert
            Assert.True(routeNodeQueryResult.IsSuccess);
            Assert.Single(routeNodeQueryResult.Value.RouteNetworkElements);

            var theRouteNodeObjectReturned = routeNodeQueryResult.Value.RouteNetworkElements[TestRouteNetwork.CO_1];

            Assert.Equal(TestRouteNetwork.CO_1, theRouteNodeObjectReturned.Id);
            Assert.Equal(RouteNetworkElementKindEnum.RouteNode, theRouteNodeObjectReturned.Kind);

            Assert.NotNull(theRouteNodeObjectReturned.Coordinates);
            Assert.NotNull(theRouteNodeObjectReturned.RouteNodeInfo);
            Assert.NotNull(theRouteNodeObjectReturned.NamingInfo);
            Assert.NotNull(theRouteNodeObjectReturned.MappingInfo);
            Assert.NotNull(theRouteNodeObjectReturned.LifecycleInfo);
            Assert.NotNull(theRouteNodeObjectReturned.SafetyInfo);
        }

        [Fact]
        public async Task QueryMultiRouteElement_ShouldReturnSuccess()
        {
            // Setup
            var routeNodeQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.CO_1, TestRouteNetwork.S13, TestRouteNetwork.S5 });

            // Act
            var queryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNodeQuery);

            // Assert
            Assert.True(queryResult.IsSuccess);
            Assert.Equal(3, queryResult.Value.RouteNetworkElements.Count);

            Assert.Equal(TestRouteNetwork.CO_1, queryResult.Value.RouteNetworkElements[TestRouteNetwork.CO_1].Id);
            Assert.Equal(RouteNetworkElementKindEnum.RouteNode, queryResult.Value.RouteNetworkElements[TestRouteNetwork.CO_1].Kind);

            Assert.Equal(TestRouteNetwork.S13, queryResult.Value.RouteNetworkElements[TestRouteNetwork.S13].Id);
            Assert.Equal(RouteNetworkElementKindEnum.RouteSegment, queryResult.Value.RouteNetworkElements[TestRouteNetwork.S13].Kind);

            Assert.Equal(TestRouteNetwork.S5, queryResult.Value.RouteNetworkElements[TestRouteNetwork.S5].Id);
            Assert.Equal(RouteNetworkElementKindEnum.RouteSegment, queryResult.Value.RouteNetworkElements[TestRouteNetwork.S5].Kind);
        }

        [Fact]
        public async Task ExplicitlyQueryCoordinatesOnly_ShouldReturnCoordinatesOnly()
        {
            // Setup
            var routeNodeQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.CO_1, TestRouteNetwork.S13 })
            {
                RouteNetworkElementFilter = new RouteNetworkElementFilterOptions()
                {
                    IncludeCoordinates = true
                },
                RelatedInterestFilter = RelatedInterestFilterOptions.None
            };

            // Act
            var queryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNodeQuery);

            // Assert
            Assert.True(queryResult.IsSuccess);
            Assert.Equal(2, queryResult.Value.RouteNetworkElements.Count);

            // CO_1
            Assert.Equal("[559485.6702553608,6209040.000026836]", queryResult.Value.RouteNetworkElements[TestRouteNetwork.CO_1].Coordinates);
            Assert.Null(queryResult.Value.RouteNetworkElements[TestRouteNetwork.CO_1].RouteNodeInfo);
            Assert.Null(queryResult.Value.RouteNetworkElements[TestRouteNetwork.CO_1].RouteSegmentInfo);
            Assert.Null(queryResult.Value.RouteNetworkElements[TestRouteNetwork.CO_1].NamingInfo);
            Assert.Null(queryResult.Value.RouteNetworkElements[TestRouteNetwork.CO_1].MappingInfo);
            Assert.Null(queryResult.Value.RouteNetworkElements[TestRouteNetwork.CO_1].LifecycleInfo);
            Assert.Null(queryResult.Value.RouteNetworkElements[TestRouteNetwork.CO_1].SafetyInfo);

            // S13
            Assert.Equal("[[559537.3506715331,6209028.300262455],[559602.7453810525,6209027.060552321]]", queryResult.Value.RouteNetworkElements[TestRouteNetwork.S13].Coordinates);
            Assert.Null(queryResult.Value.RouteNetworkElements[TestRouteNetwork.S13].RouteNodeInfo);
            Assert.Null(queryResult.Value.RouteNetworkElements[TestRouteNetwork.S13].RouteSegmentInfo);
            Assert.Null(queryResult.Value.RouteNetworkElements[TestRouteNetwork.S13].NamingInfo);
            Assert.Null(queryResult.Value.RouteNetworkElements[TestRouteNetwork.S13].MappingInfo);
            Assert.Null(queryResult.Value.RouteNetworkElements[TestRouteNetwork.S13].LifecycleInfo);
            Assert.Null(queryResult.Value.RouteNetworkElements[TestRouteNetwork.S13].SafetyInfo);
        }

        [Fact]
        public async Task ExplicitlyQueryRouteNodeInfoOnly_ShouldReturnRouteNodeInfoOnly()
        {
            // Setup
            var routeNodeQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.CO_1 })
            {
                RouteNetworkElementFilter = new RouteNetworkElementFilterOptions()
                {
                    IncludeRouteNodeInfo = true
                },
                RelatedInterestFilter = RelatedInterestFilterOptions.None
            };

            // Act
            var queryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNodeQuery);

            // Assert
            Assert.True(queryResult.IsSuccess);
            Assert.Single(queryResult.Value.RouteNetworkElements);

            var nodeFromQueryResult = queryResult.Value.RouteNetworkElements[TestRouteNetwork.CO_1];

            // Assert that route node info is returned
            Assert.NotNull(nodeFromQueryResult.RouteNodeInfo);
            Assert.Equal(RouteNodeKindEnum.CentralOfficeSmall, nodeFromQueryResult.RouteNodeInfo.Kind);
            Assert.Equal(RouteNodeFunctionEnum.SecondaryNode, nodeFromQueryResult.RouteNodeInfo.Function);

            // Assert that the rest of the information is not set
            Assert.Null(nodeFromQueryResult.Coordinates);
            Assert.Null(nodeFromQueryResult.RouteSegmentInfo);
            Assert.Null(nodeFromQueryResult.NamingInfo);
            Assert.Null(nodeFromQueryResult.MappingInfo);
            Assert.Null(nodeFromQueryResult.LifecycleInfo);
            Assert.Null(nodeFromQueryResult.SafetyInfo);
        }

        [Fact]
        public async Task ExplicitlyQueryRouteSegmentInfoOnly_ShouldReturnRouteSegmentInfoOnly()
        {
            // Setup
            var routeNodeQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.S1 })
            {
                RouteNetworkElementFilter = new RouteNetworkElementFilterOptions()
                {
                    IncludeRouteSegmentInfo = true
                },
                RelatedInterestFilter = RelatedInterestFilterOptions.None
            };

            // Act
            var queryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNodeQuery);

            // Assert
            Assert.True(queryResult.IsSuccess);
            Assert.Single(queryResult.Value.RouteNetworkElements);

            var nodeFromQueryResult = queryResult.Value.RouteNetworkElements[TestRouteNetwork.S1];

            // Assert that route node info is returned
            Assert.NotNull(nodeFromQueryResult.RouteSegmentInfo);
            Assert.Equal(RouteSegmentKindEnum.Underground, nodeFromQueryResult.RouteSegmentInfo.Kind);

            // Assert that the rest of the information is not set
            Assert.Null(nodeFromQueryResult.Coordinates);
            Assert.Null(nodeFromQueryResult.RouteNodeInfo);
            Assert.Null(nodeFromQueryResult.NamingInfo);
            Assert.Null(nodeFromQueryResult.MappingInfo);
            Assert.Null(nodeFromQueryResult.LifecycleInfo);
            Assert.Null(nodeFromQueryResult.SafetyInfo);
        }

        [Fact]
        public async Task ExplicitlyQueryNamingInfoOnly_ShouldReturnNamingInfoOnly()
        {
            // Setup
            var routeNodeQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.CO_1 })
            {
                RouteNetworkElementFilter = new RouteNetworkElementFilterOptions()
                {
                    IncludeNamingInfo = true
                },
                RelatedInterestFilter = RelatedInterestFilterOptions.None
            };

            Console.WriteLine("Was here!");
            // Act
            var queryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNodeQuery);

            // Assert
            Assert.True(queryResult.IsSuccess);
            Assert.Single(queryResult.Value.RouteNetworkElements);

            var nodeFromQueryResult = queryResult.Value.RouteNetworkElements[TestRouteNetwork.CO_1];

            // Assert that route node info is returned
            Assert.NotNull(nodeFromQueryResult.NamingInfo);
            Assert.Equal("CO-1", nodeFromQueryResult.NamingInfo.Name);
            Assert.Equal("Central Office 1", nodeFromQueryResult.NamingInfo.Description);

            // Assert that the rest of the information is not set
            Assert.Null(nodeFromQueryResult.Coordinates);
            Assert.Null(nodeFromQueryResult.RouteNodeInfo);
            Assert.Null(nodeFromQueryResult.RouteSegmentInfo);
            Assert.Null(nodeFromQueryResult.MappingInfo);
            Assert.Null(nodeFromQueryResult.LifecycleInfo);
            Assert.Null(nodeFromQueryResult.SafetyInfo);
        }

        [Fact]
        public async Task ExplicitlyQueryMappingInfoOnly_ShouldReturnMappingInfoOnly()
        {
            // Setup
            var routeNodeQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.CO_1 })
            {
                RouteNetworkElementFilter = new RouteNetworkElementFilterOptions()
                {
                    IncludeMappingInfo = true
                },
                RelatedInterestFilter = RelatedInterestFilterOptions.None
            };

            // Act
            var queryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNodeQuery);

            // Assert
            Assert.True(queryResult.IsSuccess);
            Assert.Single(queryResult.Value.RouteNetworkElements);

            var nodeFromQueryResult = queryResult.Value.RouteNetworkElements[TestRouteNetwork.CO_1];

            // Assert that route node info is returned
            Assert.NotNull(nodeFromQueryResult.MappingInfo);
            Assert.Equal(MappingMethodEnum.Schematic, nodeFromQueryResult.MappingInfo.Method);

            // Assert that the rest of the information is not set
            Assert.Null(nodeFromQueryResult.Coordinates);
            Assert.Null(nodeFromQueryResult.RouteNodeInfo);
            Assert.Null(nodeFromQueryResult.RouteSegmentInfo);
            Assert.Null(nodeFromQueryResult.NamingInfo);
            Assert.Null(nodeFromQueryResult.LifecycleInfo);
            Assert.Null(nodeFromQueryResult.SafetyInfo);
        }

        [Fact]
        public async Task ExplicitlyQueryLifecyleInfoOnly_ShouldReturnLifecyleInfoOnly()
        {
            // Setup
            var routeNodeQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.CO_1 })
            {
                RouteNetworkElementFilter = new RouteNetworkElementFilterOptions()
                {
                    IncludeLifecycleInfo = true
                },
                RelatedInterestFilter = RelatedInterestFilterOptions.None
            };

            // Act
            var queryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNodeQuery);

            // Assert
            Assert.True(queryResult.IsSuccess);
            Assert.Single(queryResult.Value.RouteNetworkElements);

            var nodeFromQueryResult = queryResult.Value.RouteNetworkElements[TestRouteNetwork.CO_1];

            // Assert that route node info is returned
            Assert.NotNull(nodeFromQueryResult.LifecycleInfo);
            Assert.Equal(DeploymentStateEnum.InService, nodeFromQueryResult.LifecycleInfo.DeploymentState);

            // Assert that the rest of the information is not set
            Assert.Null(nodeFromQueryResult.Coordinates);
            Assert.Null(nodeFromQueryResult.RouteNodeInfo);
            Assert.Null(nodeFromQueryResult.RouteSegmentInfo);
            Assert.Null(nodeFromQueryResult.NamingInfo);
            Assert.Null(nodeFromQueryResult.MappingInfo);
            Assert.Null(nodeFromQueryResult.SafetyInfo);
        }

        [Fact]
        public async Task ExplicitlyQuerySaftyInfoOnly_ShouldReturnSaftyInfoOnly()
        {
            // Setup
            var routeNodeQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.CO_1 })
            {
                RouteNetworkElementFilter = new RouteNetworkElementFilterOptions()
                {
                    IncludeSafetyInfo = true
                },
                RelatedInterestFilter = RelatedInterestFilterOptions.None
            };

            // Act
            var queryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNodeQuery);

            // Assert
            Assert.True(queryResult.IsSuccess);
            Assert.Single(queryResult.Value.RouteNetworkElements);

            var nodeFromQueryResult = queryResult.Value.RouteNetworkElements[TestRouteNetwork.CO_1];

            // Assert that route node info is returned
            Assert.NotNull(nodeFromQueryResult.SafetyInfo);
            Assert.Equal("Ikke farlig", nodeFromQueryResult.SafetyInfo.Classification);

            // Assert that the rest of the information is not set
            Assert.Null(nodeFromQueryResult.Coordinates);
            Assert.Null(nodeFromQueryResult.RouteNodeInfo);
            Assert.Null(nodeFromQueryResult.RouteSegmentInfo);
            Assert.Null(nodeFromQueryResult.NamingInfo);
            Assert.Null(nodeFromQueryResult.MappingInfo);
            Assert.Null(nodeFromQueryResult.LifecycleInfo);
        }

        [Fact]
        public async Task QueryNonExistingRouteNetworkElement_ShouldFail()
        {
            // Setup
            var routeNodeQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { Guid.NewGuid() })
            {
                RouteNetworkElementFilter = new RouteNetworkElementFilterOptions()
                {
                    IncludeSafetyInfo = true
                },
                RelatedInterestFilter = RelatedInterestFilterOptions.None
            };

            // Act
            var queryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNodeQuery);

            // Assert
            queryResult.IsFailed.Should().BeTrue();
            queryResult.Errors.OfType<GetRouteNetworkDetailsError>().Should().Contain(e => e.Code == GetRouteNetworkDetailsErrorCodes.INVALID_QUERY_ARGUMENT_ERROR_LOOKING_UP_SPECIFIED_ROUTE_NETWORK_ELEMENT_BY_ID);
        }

        [Fact]
        public async Task QueryWithNoInterestOrRouteElementIds_ShouldFail()
        {
            // Setup
            var routeNodeQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { })
            {
                RouteNetworkElementFilter = new RouteNetworkElementFilterOptions()
                {
                    IncludeSafetyInfo = true
                },
                RelatedInterestFilter = RelatedInterestFilterOptions.None
            };

            // Act
            var queryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNodeQuery);

            // Assert
            queryResult.IsFailed.Should().BeTrue();
            queryResult.Errors.OfType<GetRouteNetworkDetailsError>().Should().Contain(e => e.Code == GetRouteNetworkDetailsErrorCodes.INVALID_QUERY_ARGUMENT_NO_INTEREST_OR_ROUTE_NETWORK_IDS_SPECIFIED);
        }
    }
}
