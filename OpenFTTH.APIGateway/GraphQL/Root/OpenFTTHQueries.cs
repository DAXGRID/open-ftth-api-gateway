using GraphQL.Types;
using OpenFTTH.APIGateway.GraphQL.Addresses.Queries;
using OpenFTTH.APIGateway.GraphQL.RouteNetwork.Queries;
using OpenFTTH.APIGateway.GraphQL.Schematic.Queries;
using OpenFTTH.APIGateway.GraphQL.Search.Queries;
using OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Queries;
using OpenFTTH.APIGateway.GraphQL.Work.Queries;

namespace OpenFTTH.APIGateway.GraphQL.Root
{
    public class OpenFTTHQueries : ObjectGraphType
    {
        public OpenFTTHQueries()
        {
            Description = "GraphQL API for querying Open FTTH";

            AddField(new FieldType
            {
                Name = "apiVersion",
                Resolver = AsyncFieldResolver<StringGraphType>(context => VersionInfo.VersionString())
            });

            Field<StringGraphType>("apiVersion", resolve: context => VersionInfo.VersionString());

            Field<RouteNetworkServiceQueries>("routeNetwork", resolve: context => new { });

            Field<UtilityNetworkServiceQueries>("utilityNetwork", resolve: context => new { });

            Field<WorkServiceQueries>("workService", resolve: context => new { });

            Field<SchematicQueries>("schematic", resolve: context => new { });

            Field<SearchQueries>("search", resolve: context => new { });

            Field<AddressServiceQueries>("addressService", resolve: context => new { });
        }
    }
}
