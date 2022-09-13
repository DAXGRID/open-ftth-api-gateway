using GraphQL.Types;
using System;

namespace OpenFTTH.APIGateway.GraphQL.Root
{
    public class OpenFTTHSchema : Schema
    {
        public OpenFTTHSchema(IServiceProvider serviceProvider) : base(serviceProvider)
        {
            Query = (OpenFTTHQueries)serviceProvider.GetService(typeof(OpenFTTHQueries));
            Mutation = (OpenFTTHMutations)serviceProvider.GetService(typeof(OpenFTTHMutations));
            Subscription = (OpenFTTHSubscriptions)serviceProvider.GetService(typeof(OpenFTTHSubscriptions));
        }
    }
}
