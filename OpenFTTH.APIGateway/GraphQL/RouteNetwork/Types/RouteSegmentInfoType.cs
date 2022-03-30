using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.Events.RouteNetwork.Infos;
namespace OpenFTTH.APIGateway.GraphQL.RouteNetwork.Types
{
    public class RouteSegmentInfoType : ObjectGraphType<RouteSegmentInfo>
    {
        public RouteSegmentInfoType(ILogger<RouteSegmentInfoType> logger)
        {
            Field(x => x.Kind, type: typeof(RouteSegmentKindEnumType)).Description("The type of route segment - i.e underground, arial etc.");
            Field(x => x.Height, type: typeof(StringGraphType)).Description("The height of the route/trench.");
            Field(x => x.Width, type: typeof(StringGraphType)).Description("The width of the route/trench.");
        }
    }

    public class RouteSegmentInfoInputType : InputObjectGraphType<RouteSegmentInfo>
    {
        public RouteSegmentInfoInputType(ILogger<RouteSegmentInfoInputType> logger)
        {
            Field(x => x.Kind, type: typeof(RouteSegmentKindEnumType)).Description("The type of route segment - i.e underground, arial etc.");
            Field(x => x.Height, type: typeof(StringGraphType)).Description("The height of the route/trench.");
            Field(x => x.Width, type: typeof(StringGraphType)).Description("The width of the route/trench.");
        }
    }

}
