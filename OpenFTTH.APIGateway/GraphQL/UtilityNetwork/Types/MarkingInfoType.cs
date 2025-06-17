using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;

namespace OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types
{
    public class MarkingInfoType : ObjectGraphType<MarkingInfo>
    {
        public MarkingInfoType(ILogger<MarkingInfoType> logger)
        {
            Field(x => x.MarkingColor, type: typeof(StringGraphType)).Description("Marking color - i.e. stripe, tape etc.");
            Field(x => x.MarkingText, type: typeof(StringGraphType)).Description("Marking text - i.e. some utility custom text on the conduit/cable");
        }
    }

    public class MarkingInfoInputType : InputObjectGraphType<MarkingInfo>
    {
        public MarkingInfoInputType(ILogger<MarkingInfoType> logger)
        {
            Field(x => x.MarkingColor, type: typeof(StringGraphType)).Description("Marking color - i.e. stripe, tape etc.");
            Field(x => x.MarkingText, type: typeof(StringGraphType)).Description("Marking text - i.e. some utility custom text on the conduit/cable");
        }
    }

}
