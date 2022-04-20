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
            services.AddSingleton<TestDataMutations>();
            services.AddSingleton<SpanEquipmentMutations>();
            services.AddSingleton<TerminalEquipmentMutations>();
            services.AddSingleton<NodeContainerMutations>();

            // Queries
            services.AddSingleton<UtilityNetworkServiceQueries>();

            // Types
            services.AddSingleton<SpanEquipmentType>();
            services.AddSingleton<ManufacturerType>();
            services.AddSingleton<SpanEquipmentSpecificationType>();
            services.AddSingleton<NodeContainerSpecificationType>();
            services.AddSingleton<NodeContainerSideEnumType>();

            // Subscriptions
            services.AddSingleton<TerminalEquipmentConnectivityUpdatedSubscription>();
        }
    }
}
