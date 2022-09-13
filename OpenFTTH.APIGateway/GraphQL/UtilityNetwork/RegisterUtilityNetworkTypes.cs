using Microsoft.Extensions.DependencyInjection;
using OpenFTTH.APIGateway.GraphQL.RouteNetwork.Mutations;
using OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Queries;
using OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Subscriptions;
using OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types;

namespace OpenFTTH.APIGateway.GraphQL.UtilityNetwork
{
    public static class RegisterUtilityNetworkTypes
    {
        public static void Register(IServiceCollection services)
        {
            // Mutations
            services.AddTransient<TestDataMutations>();
            services.AddTransient<SpanEquipmentMutations>();
            services.AddTransient<TerminalEquipmentMutations>();
            services.AddTransient<NodeContainerMutations>();

            // Queries
            services.AddTransient<UtilityNetworkServiceQueries>();

            // Types
            services.AddTransient<SpanEquipmentType>();
            services.AddTransient<ManufacturerType>();
            services.AddTransient<SpanEquipmentSpecificationType>();
            services.AddTransient<NodeContainerSpecificationType>();
            services.AddTransient<NodeContainerSideEnumType>();

            // Subscriptions
            services.AddTransient<TerminalEquipmentConnectivityUpdatedSubscription>();
        }
    }
}
