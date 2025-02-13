using OpenFTTH.UtilityGraphService.API.Model.Trace;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;
using System.Collections.Generic;

namespace OpenFTTH.UtilityGraphService.Business.Trace
{
    public class SwissArmyKnifeTraceResult
    {
        public List<RouteNetworkTraceResult> RouteNetworkTraces { get; }
        public Dictionary<Guid, List<SpanSegmentRouteNetworkTraceRef>> SpanSegmentRouteNetworkTraceRefsBySpanEquipmentId { get; }
        public Dictionary<Guid, UtilityNetworkTraceResult> UtilityNetworkTraceBySpanSegmentId { get; }

        public SwissArmyKnifeTraceResult(List<RouteNetworkTraceResult> routeNetworkTraces, Dictionary<Guid, List<SpanSegmentRouteNetworkTraceRef>> spanSegmentRouteNetworkTraceRefsBySpanEquipmentId, Dictionary<Guid, UtilityNetworkTraceResult> utilityNetworkTraceBySpanSegmentId)
        {
            RouteNetworkTraces = routeNetworkTraces;
            SpanSegmentRouteNetworkTraceRefsBySpanEquipmentId = spanSegmentRouteNetworkTraceRefsBySpanEquipmentId;
            UtilityNetworkTraceBySpanSegmentId = utilityNetworkTraceBySpanSegmentId;
        }
    }
}
