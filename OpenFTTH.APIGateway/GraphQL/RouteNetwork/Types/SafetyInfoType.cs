using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.Events.Core.Infos;

namespace OpenFTTH.APIGateway.GraphQL.RouteNetwork.Types
{
    public class SafetyInfoType : ObjectGraphType<SafetyInfo>
    {
        public SafetyInfoType(ILogger<SafetyInfoType> logger)
        {
            Field(x => x.Classification, type: typeof(StringGraphType)).Description("i.e. dangerous etc.");
            Field(x => x.Remark, type: typeof(StringGraphType)).Description("Additional remark regarding safety");
        }
    }

    public class SafetyInfoInputType : InputObjectGraphType<SafetyInfo>
    {
        public SafetyInfoInputType(ILogger<NamingInfoType> logger)
        {
            Field(x => x.Classification, type: typeof(StringGraphType)).Description("i.e. dangerous etc.");
            Field(x => x.Remark, type: typeof(StringGraphType)).Description("Additional remark regarding safety");
        }
    }
}
