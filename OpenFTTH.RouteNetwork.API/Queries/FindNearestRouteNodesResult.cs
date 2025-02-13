using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.Util;
using System;
using System.Collections;
using System.Collections.Generic;

namespace OpenFTTH.RouteNetwork.API.Queries
{
    public record FindNearestRouteNodesResult
    {
        public Guid SourceRouteNodeId { get; }
        public LookupCollection<RouteNetworkElement> RouteNetworkElements { get; }
        public LookupCollection<NearestRouteNodeTraceResult> RouteNetworkTraces { get; }

        public FindNearestRouteNodesResult(Guid sourceRouteNodeId, IEnumerable<RouteNetworkElement> routeNetworkElements, IEnumerable<NearestRouteNodeTraceResult> routeNetworkTraces)
        {
            SourceRouteNodeId = sourceRouteNodeId;
            RouteNetworkElements = new LookupCollection<RouteNetworkElement>(routeNetworkElements);
            RouteNetworkTraces = new LookupCollection<NearestRouteNodeTraceResult>(routeNetworkTraces);
        }
    }
}
