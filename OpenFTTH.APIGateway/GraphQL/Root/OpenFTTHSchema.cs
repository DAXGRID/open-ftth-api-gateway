using GraphQL.Types;
using OpenFTTH.APIGateway.GraphQL.RouteNetwork.Types;
using System;

namespace OpenFTTH.APIGateway.GraphQL.Root
{
    public class OpenFTTHSchema : Schema
    {
        public OpenFTTHSchema(
            IServiceProvider serviceProvider,
            OpenFTTHQueries queries,
            OpenFTTHSubscriptions subscriptions,
            OpenFTTHMutations mutations)
            : base(serviceProvider)
        {
            Query = queries;
            Subscription = subscriptions;
            Mutation = mutations;
            RegisterType(typeof(RouteNetworkEditOperationOccuredEventType));
        }
    }
}
