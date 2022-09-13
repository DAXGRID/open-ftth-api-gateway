using Microsoft.Extensions.DependencyInjection;
using OpenFTTH.APIGateway.GraphQL.Addresses.Types;

namespace OpenFTTH.APIGateway.GraphQL.Addresses
{
    public static class RegisterAddressTypes
    {
        public static void Register(IServiceCollection services)
        {
            services.AddTransient<NearestAddressSearchHitType>();
            services.AddTransient<AccessAddressType>();
        }
    }
}
