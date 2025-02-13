using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers.Events
{
    public record NodeContainerRackSpecificationChanged : EventStoreBaseEvent
    {
        public Guid NodeContainerId { get; }
        public Guid RackId { get; }
        public Guid NewSpecificationId { get; }


        public NodeContainerRackSpecificationChanged(Guid nodeContainerId, Guid rackId, Guid newSpecificationId)
        {
            NodeContainerId = nodeContainerId;
            RackId = rackId;
            NewSpecificationId = newSpecificationId;
        }
    }
}
