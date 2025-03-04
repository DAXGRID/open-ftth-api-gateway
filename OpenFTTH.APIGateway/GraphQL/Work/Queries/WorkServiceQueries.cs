using OpenFTTH.Results;
using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.GraphQL.Work.Types;
using OpenFTTH.CQRS;
using OpenFTTH.Work.API.Model;
using OpenFTTH.Work.API.Queries;
using OpenFTTH.Work.Business;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.APIGateway.GraphQL.Work.Queries
{
    public class WorkServiceQueries : ObjectGraphType
    {
        private readonly IQueryDispatcher _queryDispatcher;

        public WorkServiceQueries(ILogger<WorkServiceQueries> logger, IQueryDispatcher queryDispatcher)
        {
            _queryDispatcher = queryDispatcher;

            Description = "GraphQL API for querying work order related data";

            Field<ListGraphType<WorkTaskAndProjectType>>(
                name: "workTasksWithProjectInformation",
                description: "Retrieve all work tasks including related project information",
                resolve: context =>
                {
                    var queryRequest = new GetAllWorkTaskAndProjects();

                    var queryResult = this._queryDispatcher.HandleAsync<GetAllWorkTaskAndProjects, Result<List<WorkTaskAndProject>>>(queryRequest).Result;

                    return queryResult.Value;
                }
            );

            Field<UserWorkContextType>(
                name: "userWorkContext",
                description: "Used to get work task status information specific to a particular user.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "userName" }),
                resolve: context =>
                {
                    var userName = context.GetArgument<string>("userName");

                    var queryRequest = new GetUserWorkContext(userName);

                    var queryResult = this._queryDispatcher.HandleAsync<GetUserWorkContext, Result<UserWorkContext>>(queryRequest).Result;

                    return queryResult.Value;
                }
            );
        }

    }
}
