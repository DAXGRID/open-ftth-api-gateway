using GraphQL.Authorization;
using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.GraphQL.RouteNetwork.Queries;
using OpenFTTH.APIGateway.GraphQL.Schematic.Queries;
using OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Queries;
using OpenFTTH.APIGateway.GraphQL.Work.Queries;

namespace OpenFTTH.APIGateway.GraphQL.Root
{
    public class OpenFTTHQueries : ObjectGraphType
    {
        public OpenFTTHQueries(ILogger<OpenFTTHQueries> logger)
        {
            Description = "GraphQL API for querying Open FTTH";

            this.AuthorizeWith("Authenticated");

            Field<StringGraphType>("apiVersion", resolve: context => VersionInfo.VersionString());

            Field<RouteNetworkServiceQueries>("routeNetwork", resolve: context => new { });

            Field<UtilityNetworkServiceQueries>("utilityNetwork", resolve: context => new { });

            Field<WorkServiceQueries>("workService", resolve: context => new { });

            Field<SchematicQueries>("schematic", resolve: context => new { });
        }

    }
}
