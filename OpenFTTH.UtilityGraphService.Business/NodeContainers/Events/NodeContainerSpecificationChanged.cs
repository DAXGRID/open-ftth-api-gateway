using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers.Events
{
    public record NodeContainerSpecificationChanged : EventStoreBaseEvent
    {
        public Guid NodeContainerId { get; }

        public Guid NewSpecificationId { get; }


        public NodeContainerSpecificationChanged(Guid nodeContainerId, Guid newSpecificationId)
        {
            NodeContainerId = nodeContainerId;
            NewSpecificationId = newSpecificationId;
        }
    }
}
