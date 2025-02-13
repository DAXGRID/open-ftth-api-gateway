using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers.Events
{
    public record NodeContainerRackHeightInUnitsChanged : EventStoreBaseEvent
    {
        public Guid NodeContainerId { get; }
        public Guid RackId { get; }
        public int NewHeightInUnits { get; }


        public NodeContainerRackHeightInUnitsChanged(Guid nodeContainerId, Guid rackId, int newHeightInUnits)
        {
            NodeContainerId = nodeContainerId;
            RackId = rackId;
            NewHeightInUnits = newHeightInUnits;
        }
    }
}
