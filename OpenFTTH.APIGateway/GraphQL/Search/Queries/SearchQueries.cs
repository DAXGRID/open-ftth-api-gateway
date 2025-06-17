using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.GraphQL.Search.Types;
using OpenFTTH.APIGateway.Search;
using OpenFTTH.APIGateway.Util;
using OpenFTTH.CQRS;
using Typesense;

namespace OpenFTTH.APIGateway.GraphQL.Search.Queries
{
    public class SearchQueries : ObjectGraphType
    {
        public SearchQueries(ILogger<SearchQueries> logger, IQueryDispatcher queryDispatcher, ITypesenseClient typesenseClient, UTM32WGS84Converter coordinateConverter)
        {
            Description = "GraphQL API for search operations";

            Field<ListGraphType<GlobalSearchHitType>>("globalSearch")
                .Description("Search for route network node, adresses etc.")
                .Arguments(new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "searchString" },
                    new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "pageSize" }
                ))
                .ResolveAsync(async (context) =>
                {
                    var searchString = context.GetArgument<string>("searchString");
                    var pageSize = context.GetArgument<int>("pageSize");
                    var searchClient = new GlobalSearchClient(typesenseClient, queryDispatcher, coordinateConverter);
                    return await searchClient.Search(searchString, pageSize);
                });
        }
    }
}
