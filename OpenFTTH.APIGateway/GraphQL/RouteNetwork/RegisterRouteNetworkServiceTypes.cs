using Microsoft.Extensions.DependencyInjection;
using OpenFTTH.APIGateway.GraphQL.RouteNetwork.Queries;
using OpenFTTH.APIGateway.GraphQL.RouteNetwork.Subscriptions;
using OpenFTTH.APIGateway.GraphQL.RouteNetwork.Types;

namespace OpenFTTH.APIGateway.GraphQL.RouteNetwork
{
    public static class RegisterRouteNetworkServiceTypes
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<RouteNetworkEventSubscription>();

            services.AddSingleton<RouteNetworkServiceQueries>();

            // General types
            services.AddSingleton<RouteNetworkEditOperationOccuredEventType>();
            services.AddSingleton<NamingInfoType>();
            services.AddSingleton<NamingInfoInputType>();

            services.AddSingleton<LifecycleInfoType>();
            services.AddSingleton<LifecycleInfoInputType>();
            services.AddSingleton<DeploymentStateEnumType>();

            services.AddSingleton<MappingInfoType>();
            services.AddSingleton<MappingInfoInputType>();
            services.AddSingleton<MappingMethodEnumType>();

            services.AddSingleton<SafetyInfoType>();
            services.AddSingleton<SafetyInfoInputType>();


            // Route node specific types
            services.AddSingleton<RouteNetworkElementType>();
            services.AddSingleton<RouteNodeInfoType>();
            services.AddSingleton<RouteNodeInfoInputType>();
            services.AddSingleton<RouteNodeKindEnumType>();
            services.AddSingleton<RouteNodeFunctionEnumType>();


            // Route segment specific types
            services.AddSingleton<RouteSegmentInfoType>();
            services.AddSingleton<RouteSegmentInfoInputType>();
            services.AddSingleton<RouteSegmentKindEnumType>();
        }
    }
}
