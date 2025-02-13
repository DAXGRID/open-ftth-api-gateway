using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers.Events
{
    public record NodeContainerTerminalEquipmentsAddedToRack : EventStoreBaseEvent
    {
        public Guid NodeContainerId { get; }
        public Guid RackId { get; }
        public int StartUnitPosition { get; }
        public Guid[] TerminalEquipmentIds { get; }
        public int TerminalEquipmentHeightInUnits { get; }

        public NodeContainerTerminalEquipmentsAddedToRack(Guid nodeContainerId, Guid rackId, int startUnitPosition, Guid[] terminalEquipmentIds, int terminalEquipmentHeightInUnits)
        {
            NodeContainerId = nodeContainerId;
            RackId = rackId;
            StartUnitPosition = startUnitPosition;
            TerminalEquipmentIds = terminalEquipmentIds;
            TerminalEquipmentHeightInUnits = terminalEquipmentHeightInUnits;
        }
    }
}
