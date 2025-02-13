using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public record SpanSegmentRouteNetworkTraceRef
    {
        public Guid SpanEquipmentOrSegmentId { get; }
        public Guid TraceId { get; }

        public SpanSegmentRouteNetworkTraceRef(Guid spanEquipmentOrSegmentId, Guid traceId)
        {
            SpanEquipmentOrSegmentId = spanEquipmentOrSegmentId;
            TraceId = traceId;
        }
    }
}
