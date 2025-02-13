using FluentResults;
using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.RouteNetwork.API.Queries
{
    /// <summary>
    /// Used to find the shortest path between two route nodes in the route network
    /// </summary>
    public class ShortestPathBetweenRouteNodes : IQuery<Result<ShortestPathBetweenRouteNodesResult>>
    {
        public static string RequestName => typeof(ShortestPathBetweenRouteNodes).Name;
        public Guid SourceRouteNodeId { get; }
        public Guid DestRouteNodeId { get; }

        public ShortestPathBetweenRouteNodes(Guid sourceRouteNodeId, Guid destRouteNodeId)
        {
            SourceRouteNodeId = sourceRouteNodeId;
            DestRouteNodeId = destRouteNodeId;
        }
    }
}
