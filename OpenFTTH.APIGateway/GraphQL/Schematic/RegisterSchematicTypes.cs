using Microsoft.Extensions.DependencyInjection;
using OpenFTTH.APIGateway.GraphQL.RouteNetwork.Mutations;
using OpenFTTH.APIGateway.GraphQL.Schematic.Queries;
using OpenFTTH.APIGateway.GraphQL.Schematic.Subscriptions;
using OpenFTTH.APIGateway.GraphQL.Schematic.Types;

namespace OpenFTTH.APIGateway.GraphQL.Schematic
{
    public static class RegisterSchematicTypes
    {
        public static void Register(IServiceCollection services)
        {
            services.AddTransient<SchematicMutations>();
            services.AddTransient<SchematicQueries>();
            services.AddTransient<SchematicUpdatedSubscription>();

            services.AddTransient<DiagramType>();
            services.AddTransient<DiagramObjectType>();
        }
    }
}
