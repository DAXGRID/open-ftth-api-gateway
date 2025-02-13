using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers.Events
{
    public record NodeContainerTerminalEquipmentMovedToRack : EventStoreBaseEvent
    {
        public Guid NodeContainerId { get; }
        public Guid OldRackId { get; }
        public Guid NewRackId { get; }
        public int StartUnitPosition { get; }
        public Guid TerminalEquipmentId { get; }
        public int TerminalEquipmentHeightInUnits { get; }

        public NodeContainerTerminalEquipmentMovedToRack(Guid nodeContainerId, Guid oldRackId, Guid newRackId, int startUnitPosition, Guid terminalEquipmentId, int terminalEquipmentHeightInUnits)
        {
            NodeContainerId = nodeContainerId;
            OldRackId = oldRackId;
            NewRackId = newRackId;
            StartUnitPosition = startUnitPosition;
            TerminalEquipmentId = terminalEquipmentId;
            TerminalEquipmentHeightInUnits = terminalEquipmentHeightInUnits;
        }
    }
}
