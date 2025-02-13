using System;
using System.Collections.Generic;

namespace OpenFTTH.RouteNetwork.API.Queries
{
    public record ShortestPathBetweenRouteNodesResult
    {
        public List<Guid> RouteNetworkElementIds { get; }

        public long ElapsedTimeMs { get; }

        public ShortestPathBetweenRouteNodesResult(List<Guid> routeNetworkElementIds, long elapsedTimeMs)
        {
            RouteNetworkElementIds = routeNetworkElementIds;
            ElapsedTimeMs = elapsedTimeMs;
        }
    }
}
