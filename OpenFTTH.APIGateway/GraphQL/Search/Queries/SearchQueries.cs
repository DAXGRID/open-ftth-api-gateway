using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.GraphQL.Search.Types;
using OpenFTTH.APIGateway.Search;
using OpenFTTH.CQRS;
using Typesense;

namespace OpenFTTH.APIGateway.GraphQL.Search.Queries
{
    public class SearchQueries : ObjectGraphType
    {
        public SearchQueries(ILogger<SearchQueries> logger, IQueryDispatcher queryDispatcher, ITypesenseClient typesenseClient)
        {
            Description = "GraphQL API for search operations";

            FieldAsync<ListGraphType<GlobalSearchHitType>>(
                name: "globalSearch",
                description: "Search for route network node, adresses etc.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "searchString" },
                    new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "pageSize" }
                ),
                resolve: async (context) =>
                {
                    var searchString = context.GetArgument<string>("searchString");
                    var pageSize = context.GetArgument<int>("pageSize");
                    var searchClient = new GlobalSearchClient(typesenseClient, queryDispatcher);
                    return await searchClient.Search(searchString, pageSize);
                }
            );
        }
    }
}
