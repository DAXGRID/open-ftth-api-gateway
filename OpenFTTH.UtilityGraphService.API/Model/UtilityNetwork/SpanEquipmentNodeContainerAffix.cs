using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public record SpanEquipmentNodeContainerAffix
    {
        public Guid RouteNodeId { get; }
        public Guid NodeContainerId { get; }
        public NodeContainerSideEnum NodeContainerIngoingSide { get; }

        public SpanEquipmentNodeContainerAffix(Guid routeNodeId, Guid nodeContainerId, NodeContainerSideEnum nodeContainerIngoingSide)
        {
            RouteNodeId = routeNodeId;
            NodeContainerId = nodeContainerId;
            NodeContainerIngoingSide = nodeContainerIngoingSide;
        }
    }
}
