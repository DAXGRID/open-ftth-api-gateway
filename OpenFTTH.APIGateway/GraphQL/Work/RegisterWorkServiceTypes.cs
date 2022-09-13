using Microsoft.Extensions.DependencyInjection;
using OpenFTTH.APIGateway.GraphQL.Work.Mutations;
using OpenFTTH.APIGateway.GraphQL.Work.Queries;
using OpenFTTH.APIGateway.GraphQL.Work.Types;

namespace OpenFTTH.APIGateway.GraphQL.Work
{
    public static class RegisterWorkServiceTypes
    {
        public static void Register(IServiceCollection services)
        {
            // Queries
            services.AddTransient<WorkServiceQueries>();

            // Mutations
            services.AddTransient<UserWorkContextMutations>();

            // Work specific types
            services.AddTransient<WorkTaskType>();
            services.AddTransient<UserWorkContextType>();
        }
    }
}
