using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers.Events
{
    public record NodeContainerTerminalsDisconnected : EventStoreBaseEvent
    {
        public Guid NodeContainerId { get; }
        public Guid FromTerminalEquipmentId { get; }
        public Guid FromTerminalId { get; }
        public Guid ToTerminalEquipmentId { get; }
        public Guid ToTerminalId { get; }

        public NodeContainerTerminalsDisconnected(Guid nodeContainerId, Guid fromTerminalEquipmentId, Guid fromTerminalId, Guid toTerminalEquipmentId, Guid toTerminalId)
        {
            NodeContainerId = nodeContainerId;
            FromTerminalEquipmentId = fromTerminalEquipmentId;
            FromTerminalId = fromTerminalId;
            ToTerminalEquipmentId = toTerminalEquipmentId;
            ToTerminalId = toTerminalId;
        }
    }
}
