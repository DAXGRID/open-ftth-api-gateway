using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers.Events
{
    public record NodeContainerTerminalEquipmentReferenceRemoved : EventStoreBaseEvent
    {

        public Guid NodeContainerId { get; }
        public Guid TerminalEquipmentId { get; }

        public NodeContainerTerminalEquipmentReferenceRemoved(Guid nodeContainerId, Guid terminalEquipmentId)
        {
            NodeContainerId = nodeContainerId;
            TerminalEquipmentId = terminalEquipmentId;
        }
    }
}
