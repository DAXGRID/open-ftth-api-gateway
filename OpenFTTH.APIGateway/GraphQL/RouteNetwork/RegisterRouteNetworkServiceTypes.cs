using Microsoft.Extensions.DependencyInjection;
using OpenFTTH.APIGateway.GraphQL.RouteNetwork.Queries;
using OpenFTTH.APIGateway.GraphQL.RouteNetwork.Types;

namespace OpenFTTH.APIGateway.GraphQL.RouteNetwork
{
    public static class RegisterRouteNetworkServiceTypes
    {
        public static void Register(IServiceCollection services)
        {
            services.AddTransient<RouteNetworkServiceQueries>();

            // General types
            services.AddTransient<RouteNetworkEditOperationOccuredEventType>();
            services.AddTransient<NamingInfoType>();
            services.AddTransient<NamingInfoInputType>();

            services.AddTransient<LifecycleInfoType>();
            services.AddTransient<LifecycleInfoInputType>();
            services.AddTransient<DeploymentStateEnumType>();

            services.AddTransient<MappingInfoType>();
            services.AddTransient<MappingInfoInputType>();
            services.AddTransient<MappingMethodEnumType>();

            services.AddTransient<SafetyInfoType>();
            services.AddTransient<SafetyInfoInputType>();


            // Route node specific types
            services.AddTransient<RouteNetworkElementType>();
            services.AddTransient<RouteNodeInfoType>();
            services.AddTransient<RouteNodeInfoInputType>();
            services.AddTransient<RouteNodeKindEnumType>();
            services.AddTransient<RouteNodeFunctionEnumType>();


            // Route segment specific types
            services.AddTransient<RouteSegmentInfoType>();
            services.AddTransient<RouteSegmentInfoInputType>();
            services.AddTransient<RouteSegmentKindEnumType>();
        }
    }
}
