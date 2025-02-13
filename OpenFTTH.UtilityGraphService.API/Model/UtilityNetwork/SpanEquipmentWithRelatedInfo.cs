using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public record SpanEquipmentWithRelatedInfo : SpanEquipment
    {
        public SpanSegmentRouteNetworkTraceRef[]? RouteNetworkTraceRefs { get; set; }

        public SpanEquipmentWithRelatedInfo(SpanEquipment original) : base(original) { }
    }
}
