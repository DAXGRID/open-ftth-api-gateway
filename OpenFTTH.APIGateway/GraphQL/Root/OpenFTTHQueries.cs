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

            Field<StringGraphType>("apiVersion").Resolve(_ => VersionInfo.VersionString());

            Field<RouteNetworkServiceQueries>("routeNetwork").Resolve(_ => new { });

            Field<UtilityNetworkServiceQueries>("utilityNetwork").Resolve(_ => new { });

            Field<WorkServiceQueries>("workService").Resolve(_ => new { });

            Field<SchematicQueries>("schematic").Resolve(_ => new { });

            Field<SearchQueries>("search").Resolve(_ => new { });

            Field<AddressServiceQueries>("addressService").Resolve(_ => new { });
        }
    }
}
