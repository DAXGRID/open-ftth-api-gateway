﻿using FluentResults;
using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.GraphQL.Outage.Types;
using OpenFTTH.APIGateway.GraphQL.Work.Types;
using OpenFTTH.CQRS;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.Outage;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.Work.API.Model;
using OpenFTTH.Work.API.Queries;
using OpenFTTH.Work.Business;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.APIGateway.GraphQL.Outage.Queries
{
    public class OutageServiceQueries : ObjectGraphType
    {
        private readonly IQueryDispatcher _queryDispatcher;

        public OutageServiceQueries(ILogger<OutageServiceQueries> logger, IQueryDispatcher queryDispatcher)
        {
            _queryDispatcher = queryDispatcher;

            Description = "GraphQL API for querying outage / trouble ticket related data";

            Field<ListGraphType<WorkTaskAndProjectType>>(
                 name: "latestTenTroubleTicketsOrderedByDate",
                 description: "Retrieve the latest 10 trouble ticket work tasks ordered by date",
                 resolve: context =>
                 {
                     var queryRequest = new GetAllWorkTaskAndProjects();

                     var queryResult = this._queryDispatcher.HandleAsync<GetAllWorkTaskAndProjects, Result<List<WorkTaskAndProject>>>(queryRequest).Result;

                     var orderedTroubleTicketWorkTrask = queryResult.Value.Where(w => w.WorkTask.Type != null && w.WorkTask.Type == "Trouble ticket").OrderByDescending(w => w.WorkTask.CreatedDate).Take(10);

                     return orderedTroubleTicketWorkTrask;
                 }
             );

            FieldAsync<OutageViewNodeType>(
                name: "outageView",
                description: "Information needed to show outage tree to the user",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNetworkElementId" },
                    new QueryArgument<IdGraphType> { Name = "equipmentId" }
                ),
                resolve: async context =>
                {
                    var routeNetworkElementId = context.GetArgument<Guid>("routeNetworkElementId");

                    if (context.HasArgument("equipmentId"))
                    {
                        var equipmentId = context.GetArgument<Guid>("equipmentId");

                        // TODO: Replace test data with call to utility network query
                        var rootNode = new OutageViewNode(Guid.NewGuid(), "root node", "root node eq: " + equipmentId.ToString());

                        // leaf 1
                        var leaf1 = new OutageViewNode(Guid.NewGuid(), "leaf 1 (expanded)");
                        leaf1.Expanded = true;
                        rootNode.AddNode(leaf1);

                        leaf1.AddNode(new OutageViewNode(Guid.NewGuid(), "child 1"));
                        leaf1.AddNode(new OutageViewNode(Guid.NewGuid(), "child 2"));


                        // leaf 2
                        var leaf2 = new OutageViewNode(Guid.NewGuid(), "leaf 1 (not expanded)");
                        rootNode.AddNode(leaf2);

                        leaf2.AddNode(new OutageViewNode(Guid.NewGuid(), "child 1"));
                        leaf2.AddNode(new OutageViewNode(Guid.NewGuid(), "child 2"));

                        return rootNode;

                    }


                    var getOutageViewQuery = new GetOutageView(routeNetworkElementId);

                    var queryResult = await queryDispatcher.HandleAsync<GetOutageView, Result<OutageViewNode>>(getOutageViewQuery);

                    if (queryResult.IsFailed)
                    {
                        foreach (var error in queryResult.Errors)
                            context.Errors.Add(new ExecutionError(error.Message));

                        return null;
                    }

                    return queryResult.Value;
                }
             );
        }

    }
}
