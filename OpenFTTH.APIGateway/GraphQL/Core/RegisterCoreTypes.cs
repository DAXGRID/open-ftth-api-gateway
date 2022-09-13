using Microsoft.Extensions.DependencyInjection;

namespace OpenFTTH.APIGateway.CoreTypes
{
    public static class RegisterCoreTypes
    {
        public static void Register(IServiceCollection services)
        {
            services.AddTransient<CommandResultType>();
            services.AddTransient<GeometryType>();
        }
    }
}
