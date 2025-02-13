using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers.Events
{
    public record NodeContainerTerminalEquipmentAdded : EventStoreBaseEvent
    {
        public Guid NodeContainerId { get; }
        public Guid TerminalEquipmentId { get; }

        public NodeContainerTerminalEquipmentAdded(Guid nodeContainerId, Guid terminalEquipmentId)
        {
            NodeContainerId = nodeContainerId;
            TerminalEquipmentId = terminalEquipmentId;
        }
    }
}
