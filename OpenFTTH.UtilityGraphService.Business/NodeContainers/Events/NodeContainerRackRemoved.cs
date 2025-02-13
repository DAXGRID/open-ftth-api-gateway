using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers.Events
{
    public record NodeContainerRackRemoved : EventStoreBaseEvent
    {
        public Guid NodeContainerId { get; }
        public Guid RackId { get; }

        public NodeContainerRackRemoved(Guid nodeContainerId, Guid rackId)
        {
            NodeContainerId = nodeContainerId;
            RackId = rackId;
        }
    }
}
