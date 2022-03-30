using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.Events.Core.Infos;
namespace OpenFTTH.APIGateway.GraphQL.RouteNetwork.Types
{
    public class NamingInfoType : ObjectGraphType<NamingInfo>
    {
        public NamingInfoType(ILogger<NamingInfoType> logger)
        {
            Field(x => x.Name, type: typeof(StringGraphType)).Description("Short name");
            Field(x => x.Description, type: typeof(StringGraphType)).Description("Long name");
        }
    }

    public class NamingInfoInputType : InputObjectGraphType<NamingInfo>
    {
        public NamingInfoInputType(ILogger<NamingInfoType> logger)
        {
            Field(x => x.Name, type: typeof(StringGraphType)).Description("Short name");
            Field(x => x.Description, type: typeof(StringGraphType)).Description("Long name");
        }
    }

}
