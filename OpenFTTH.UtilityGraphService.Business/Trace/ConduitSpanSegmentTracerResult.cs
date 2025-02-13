using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.UtilityGraphService.API.Model.Trace;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;
using System.Collections.Generic;

namespace OpenFTTH.UtilityGraphService.Business.Trace
{
    public class ConduitSpanSegmentTracerResult
    {
        public RouteNetworkElementIdList RouteNetworkWalk { get; }
        public UtilityNetworkTraceResult? UtilityNetworkTrace { get; }

        public ConduitSpanSegmentTracerResult(RouteNetworkElementIdList routeNetworkWalk, UtilityNetworkTraceResult? utilityNetworkTrace)
        {
            RouteNetworkWalk = routeNetworkWalk;
            UtilityNetworkTrace = utilityNetworkTrace;
        }
    }
}

