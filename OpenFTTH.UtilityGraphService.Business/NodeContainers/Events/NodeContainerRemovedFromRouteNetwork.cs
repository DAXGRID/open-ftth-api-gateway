using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers.Events
{
    public record NodeContainerRemovedFromRouteNetwork : EventStoreBaseEvent
    {
        public Guid NodeContainerId { get; }

        public NodeContainerRemovedFromRouteNetwork(Guid nodeContainerId)
        {
            NodeContainerId = nodeContainerId;
        }
    }
}
