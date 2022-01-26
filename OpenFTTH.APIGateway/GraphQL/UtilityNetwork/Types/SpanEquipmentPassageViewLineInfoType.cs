﻿using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;

namespace OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types
{
    public class SpanEquipmentPassageViewLineInfoType : ObjectGraphType<SpanEquipmentPassageViewLineInfo>
    {
        public SpanEquipmentPassageViewLineInfoType(ILogger<SpanEquipmentPassageViewLineInfoType> logger)
        {
            Field(x => x.SpanSegmentId, type: typeof(IdGraphType)).Description("Span segment id");
            Field(x => x.From, type: typeof(StringGraphType)).Description("From");
            Field(x => x.To, type: typeof(StringGraphType)).Description("To");
            Field(x => x.ConduitId, type: typeof(StringGraphType)).Description("Span equipment name");
            Field(x => x.OuterConduitInfo, type: typeof(StringGraphType)).Description("Outer conduit info");
            Field(x => x.InnerConduitInfo, type: typeof(StringGraphType)).Description("Inner conduit info");
            Field(x => x.SegmentLength, type: typeof(FloatGraphType)).Description("Segment length");
            Field(x => x.CumulativeDistance, type: typeof(FloatGraphType)).Description("Cumulative distance");
            Field(x => x.RouteSegmentIds, type: typeof(ListGraphType<IdGraphType>)).Description("Route network segment ids of the span segment traversal");
            Field(x => x.RouteSegmentGeometries, type: typeof(ListGraphType<StringGraphType>)).Description("Route network segment geometries of the span segment traversal");
        }
    }
}
