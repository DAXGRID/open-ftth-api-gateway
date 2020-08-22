﻿using Microsoft.Extensions.DependencyInjection;
using OpenFTTH.APIGateway.RouteNetwork.GraphQL.Mutations;
using OpenFTTH.APIGateway.RouteNetwork.GraphQL.Queries;
using OpenFTTH.APIGateway.RouteNetwork.GraphQL.Subscriptions;
using OpenFTTH.APIGateway.RouteNetwork.GraphQL.Types;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenFTTH.APIGateway.RouteNetwork.GraphQL
{
    public static class ConfigureRouteNetworkService
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<RouteNetworkEventSubscription>();

            services.AddSingleton<RouteNetworkServiceQueries>();
            services.AddSingleton<RouteNodeMutations>();

            // General types
            services.AddSingleton<RouteNetworkEventType>();
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
            services.AddSingleton<RouteNodeType>();
            services.AddSingleton<RouteNodeInfoType>();
            services.AddSingleton<RouteNodeInfoInputType>();
            services.AddSingleton<RouteNodeKindEnumType>();
            services.AddSingleton<RouteNodeFunctionEnumType>();

        }
    }
}