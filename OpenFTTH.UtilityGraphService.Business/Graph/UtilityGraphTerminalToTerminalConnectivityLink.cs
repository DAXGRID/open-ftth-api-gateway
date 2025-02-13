using DAX.ObjectVersioning.Graph;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.Business.Graph
{
    public class UtilityGraphTerminalToTerminalConnectivityLink : GraphEdge, IUtilityGraphTerminalToTerminalConnectivityRef
    {
        public Guid RouteNodeId { get; }
        public double FiberCoordLength { get; }

        public UtilityGraphTerminalToTerminalConnectivityLink(Guid id, GraphNode? fromNode, GraphNode? toNode, Guid routeNodeId, double fiberCoordLength) : base(id, fromNode, toNode)
        {
            RouteNodeId = routeNodeId;
            FiberCoordLength = fiberCoordLength;
        }

        public override string ToString()
        {
            return $"Terminal to terminal connection at node: {RouteNodeId}";
        }
    }
}

