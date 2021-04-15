using GraphQL.Authorization;
using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.GraphQL.RouteNetwork.Subscriptions;
using OpenFTTH.APIGateway.GraphQL.Schematic.Subscriptions;

namespace OpenFTTH.APIGateway.GraphQL.Root
{
    public class OpenFTTHSubscriptions : ObjectGraphType
    {
        public OpenFTTHSubscriptions(ILogger<OpenFTTHSubscriptions> logger, RouteNetworkEventSubscription routeNetworkEventSubscription, SchematicUpdatedSubscription schematicUpdatedSubscription)
        {
            Description = "GraphQL API for subscriping to various events in the Open FTTH system";

            this.AuthorizeWith("Authenticated");

            routeNetworkEventSubscription.AddFields(this);
            schematicUpdatedSubscription.AddFields(this);
        }
    }
}
