﻿using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.Remote;
using OpenFTTH.APIGateway.RouteNetwork.GraphQL.Mutations;
using OpenFTTH.APIGateway.RouteNetwork.GraphQL.Test;
using OpenFTTH.APIGateway.RouteNetwork.GraphQL.Types;
using OpenFTTH.RouteNetworkService.Queries;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenFTTH.APIGateway.RouteNetwork.GraphQL.Queries
{
    public class RouteNetworkServiceQueries : ObjectGraphType
    {
        public RouteNetworkServiceQueries(ILogger<RouteNetworkServiceQueries> logger, QueryServiceClient<RouteNetworkServiceQueries> routeNetworkQueries)
        {
            Description = "GraphQL API for querying data owned by route nodes and route segments";

            Field<RouteNodeType>(
                "routeNode",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "Id" }),
                resolve: context =>
                {
                    Guid id;
                    if (!Guid.TryParse(context.GetArgument<string>("id"), out id))
                    {
                        context.Errors.Add(new ExecutionError("Wrong value for guid"));
                        return null;
                    }

                    logger.LogDebug("Route node query: " + id);

                    // For quick testing... should be removed
                    if (RouteNodeState.State.ContainsKey(id))
                    {
                        logger.LogDebug("Got a test id. Will therefore *not* call the route network service, but just fetch data from an in-memory state!");
                        return RouteNodeState.State[id];
                    }


                    var routeNodeData = routeNetworkQueries.Query<RouteNodeQuery, RouteNodeQueryResult>(new RouteNodeQuery(id));

                    return routeNodeData;
                }
            );
        
        }

    }
}
