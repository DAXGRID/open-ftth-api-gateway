using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers.Events
{
    public record NodeContainerManufacturerChanged : EventStoreBaseEvent
    {
        public Guid NodeContainerId { get; }

        public Guid ManufacturerId { get; }

        public NodeContainerManufacturerChanged(Guid nodeContainerId, Guid manufacturerId)
        {
            NodeContainerId = nodeContainerId;
            ManufacturerId = manufacturerId;
        }
    }
}
