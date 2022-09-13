using GraphQL.Types;
using System;

namespace OpenFTTH.APIGateway.GraphQL.Root
{
    public class OpenFTTHSchema : Schema
    {
        public OpenFTTHSchema(IServiceProvider serviceProvider) : base(serviceProvider)
        {
            Query = new AutoRegisteringObjectGraphType<OpenFTTHQueries>();
            Mutation = new AutoRegisteringObjectGraphType<OpenFTTHMutations>();
            Subscription = new AutoRegisteringObjectGraphType<OpenFTTHSubscriptions>();
        }
    }
}
