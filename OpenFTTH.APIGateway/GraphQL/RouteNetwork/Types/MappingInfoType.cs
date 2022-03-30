using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.Events.Core.Infos;

namespace OpenFTTH.APIGateway.GraphQL.RouteNetwork.Types
{
    public class MappingInfoType : ObjectGraphType<MappingInfo>
    {
        public MappingInfoType(ILogger<MappingInfoType> logger)
        {
            Field(x => x.Method, type: typeof(MappingMethodEnumType)).Description("Digitizing/mapping method.");
            Field(x => x.HorizontalAccuracy, type: typeof(StringGraphType)).Description("The horizontal accuracy in milimeter");
            Field(x => x.VerticalAccuracy, type: typeof(StringGraphType)).Description("The vertical accuracy in milimeter");
            Field(x => x.SurveyDate, type: typeof(DateTimeGraphType)).Description("The date when the asset was surveyed.");
            Field(x => x.SourceInfo, type: typeof(StringGraphType)).Description("Source of measurement/surveying");
        }
    }

    public class MappingInfoInputType : InputObjectGraphType<MappingInfo>
    {
        public MappingInfoInputType(ILogger<MappingInfoInputType> logger)
        {
            Field(x => x.Method, type: typeof(MappingMethodEnumType)).Description("Digitizing/mapping method.");
            Field(x => x.HorizontalAccuracy, type: typeof(StringGraphType)).Description("The horizontal accuracy in milimeter");
            Field(x => x.VerticalAccuracy, type: typeof(StringGraphType)).Description("The vertical accuracy in milimeter");
            Field(x => x.SurveyDate, type: typeof(DateTimeGraphType)).Description("The date when the asset was surveyed.");
            Field(x => x.SourceInfo, type: typeof(StringGraphType)).Description("Source of measurement/surveying");
        }
    }

}
