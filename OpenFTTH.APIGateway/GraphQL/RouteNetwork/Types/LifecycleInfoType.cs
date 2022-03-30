using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.Events.Core.Infos;

namespace OpenFTTH.APIGateway.GraphQL.RouteNetwork.Types
{
    public class LifecycleInfoType : ObjectGraphType<LifecycleInfo>
    {
        public LifecycleInfoType(ILogger<LifecycleInfoType> logger)
        {
            Field(x => x.DeploymentState, type: typeof(DeploymentStateEnumType)).Description("Current deployment state of the asset - i.e. in service, out of service, disposed etc.");
            Field(x => x.InstallationDate, type: typeof(DateTimeGraphType)).Description("the date when the asset was installed");
            Field(x => x.RemovalDate, type: typeof(DateTimeGraphType)).Description("The date when the asset was removed.");
        }
    }

    public class LifecycleInfoInputType : InputObjectGraphType<LifecycleInfo>
    {
        public LifecycleInfoInputType(ILogger<LifecycleInfoInputType> logger)
        {
            Field(x => x.DeploymentState, type: typeof(DeploymentStateEnumType)).Description("Current deployment state of the asset - i.e. in service, out of service, disposed etc.");
            Field(x => x.InstallationDate, type: typeof(DateTimeGraphType)).Description("the date when the asset was installed");
            Field(x => x.RemovalDate, type: typeof(DateTimeGraphType)).Description("The date when the asset was removed.");
        }
    }

}
