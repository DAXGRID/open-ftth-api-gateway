using GraphQL.Types;
using Microsoft.Extensions.Logging;

namespace OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types
{
    public class SpanSegmentTraceType : ObjectGraphType<SpanSegmentTrace>
    {
        public SpanSegmentTraceType(ILogger<SpanSegmentTraceType> logger)
        {
            Field(x => x.RouteNetworkSegmentIds, type: typeof(ListGraphType<IdGraphType>)).Description("Route network segment ids of the span segment traversal");

            Field(x => x.RouteNetworkSegmentGeometries, type: typeof(ListGraphType<StringGraphType>)).Description("Route network segment geometries of the span segment traversal");
        }
    }
}
