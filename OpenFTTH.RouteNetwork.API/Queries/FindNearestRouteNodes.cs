using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.RouteNetwork.Infos;
using System;

namespace OpenFTTH.RouteNetwork.API.Queries
{
    /// <summary>
    /// Used to find nearest route network nodes from a source node
    /// </summary>
    public class FindNearestRouteNodes : IQuery<Result<FindNearestRouteNodesResult>>
    {
        public static string RequestName => typeof(FindNearestRouteNodes).Name;
        public Guid SourceRouteNodeId { get; }
        public int MaxHits { get; }
        public double SearchRadiusMeters { get; }
        public RouteNodeKindEnum[] NodeKindStops { get; }
        public RouteNodeKindEnum[] NodeKindOfInterests { get; }

        #region Route Network Element Filter Options
        private RouteNetworkElementFilterOptions _routeNetworkElementFilterOptions =
            new RouteNetworkElementFilterOptions()
            {
                IncludeRouteNodeInfo = true,
                IncludeRouteSegmentInfo = true,
                IncludeCoordinates = true,
                IncludeNamingInfo = true,
                IncludeMappingInfo = true,
                IncludeLifecycleInfo = true,
                IncludeSafetyInfo = true
            };

        public RouteNetworkElementFilterOptions RouteNetworkElementFilter
        {
            get { return _routeNetworkElementFilterOptions; }
            init { _routeNetworkElementFilterOptions = value; }
        }
        #endregion

        public FindNearestRouteNodes(Guid sourceRouteNodeId, int maxHits, double searchRadiusMeters, RouteNodeKindEnum[] nodeKindStops, RouteNodeKindEnum[] nodeKindOfInterests)
        {
            SourceRouteNodeId = sourceRouteNodeId;
            MaxHits = maxHits;
            SearchRadiusMeters = searchRadiusMeters;
            NodeKindStops = nodeKindStops;
            NodeKindOfInterests = nodeKindOfInterests;
        }
    }
}
