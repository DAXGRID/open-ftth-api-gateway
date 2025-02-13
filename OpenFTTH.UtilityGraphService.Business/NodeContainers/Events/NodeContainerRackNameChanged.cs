using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers.Events
{
    public record NodeContainerRackNameChanged : EventStoreBaseEvent
    {
        public Guid NodeContainerId { get; }
        public Guid RackId { get; }
        public string NewName { get; }


        public NodeContainerRackNameChanged(Guid nodeContainerId, Guid rackId, string newName)
        {
            NodeContainerId = nodeContainerId;
            RackId = rackId;
            NewName = newName;
        }
    }
}
