using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.GraphQL.RouteNetwork.Types;
using OpenFTTH.CQRS;
using OpenFTTH.Events.RouteNetwork.Infos;
using OpenFTTH.Results;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.RouteNetwork.Business.RouteElements.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.APIGateway.GraphQL.RouteNetwork.Queries
{
    public class RouteNetworkServiceQueries : ObjectGraphType
    {
        public RouteNetworkServiceQueries(ILogger<RouteNetworkServiceQueries> logger, IQueryDispatcher queryDispatcher)
        {
            Description = "GraphQL API for querying data owned by route nodes and route segments";

            Field<RouteNetworkElementType>("routeElement")
                .Arguments(new QueryArguments(new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "Id" }))
                .ResolveAsync(async context =>
                {
                    if (!Guid.TryParse(context.GetArgument<string>("id"), out Guid routeNodeId))
                    {
                        context.Errors.Add(new ExecutionError("Wrong value for guid"));
                        return null;
                    }

                    var routeNodeQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { routeNodeId })
                    {
                        RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementOnly
                    };

                    var queryResult = await queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNodeQuery);

                    if (queryResult.IsFailed)
                    {
                        context.Errors.Add(new ExecutionError(queryResult.Errors.First().Message));
                        return null;
                    }

                    return queryResult.Value.RouteNetworkElements[routeNodeId];
                });

            Field<ListGraphType<IdGraphType>>("shortestPathBetweenSegments")
                .Description("Returns the shortest path (route segment ids) between two segments, including those segments.")
                .Arguments(
                    new QueryArguments(
                        new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "fromSegmentId" },
                        new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "toSegmentId" }
                    )
                )
                .ResolveAsync(async context =>
                {
                    var fromSegmentId = context.GetArgument<Guid>("fromSegmentId");
                    var toSegmentId = context.GetArgument<Guid>("toSegmentId");

                    var shortestPathQuery = new ShortestPathBetweenRouteSegments(fromSegmentId, toSegmentId);

                    var shortestPathBetweenSegmentsResult = await queryDispatcher
                        .HandleAsync<ShortestPathBetweenRouteSegments, Result<ShortestPathBetweenRouteSegmentsResult>>(shortestPathQuery)
                        .ConfigureAwait(false);

                    if (shortestPathBetweenSegmentsResult.IsFailed)
                    {
                        context.Errors.Add(new ExecutionError(shortestPathBetweenSegmentsResult.Errors.First().Message));
                        return null;
                    }

                    return shortestPathBetweenSegmentsResult.Value.RouteSegmentElementIds;
                });

            Field<ListGraphType<RouteNetworkTraceType>>("nearestNeighborNodes")
                 .Arguments(new QueryArguments(
                     new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "sourceRouteNodeId" },
                     new QueryArgument<NonNullGraphType<ListGraphType<RouteNodeKindEnumType>>> { Name = "stops" },
                     new QueryArgument<NonNullGraphType<ListGraphType<RouteNodeKindEnumType>>> { Name = "interests" },
                     new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "maxHits" },
                     new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "maxBirdFlyDistanceMeters" }
                 ))
                 .ResolveAsync(async context =>
                 {
                     Guid routeNodeId = context.GetArgument<Guid>("sourceRouteNodeId");
                     List<RouteNodeKindEnum> stops = context.GetArgument<List<RouteNodeKindEnum>>("stops");
                     List<RouteNodeKindEnum> interests = context.GetArgument<List<RouteNodeKindEnum>>("interests");
                     int maxHits = context.GetArgument<int>("maxHits");
                     int maxBirdFlyDistanceMeters = context.GetArgument<int>("maxBirdFlyDistanceMeters");


                     var nearestNodeQuery = new FindNearestRouteNodes(routeNodeId, maxHits, maxBirdFlyDistanceMeters, stops.ToArray(), interests.ToArray());

                     var nearestNodeQueryResult = await queryDispatcher.HandleAsync<FindNearestRouteNodes, Result<FindNearestRouteNodesResult>>(nearestNodeQuery);

                     if (nearestNodeQueryResult.IsFailed)
                     {
                         context.Errors.Add(new ExecutionError(nearestNodeQueryResult.Errors.First().Message));
                         return null;
                     }

                     return nearestNodeQueryResult.Value.RouteNetworkTraces;
                 });
        }
    }
}
