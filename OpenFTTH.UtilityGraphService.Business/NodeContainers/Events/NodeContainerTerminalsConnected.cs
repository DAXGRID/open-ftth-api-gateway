using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers.Events
{
    public record NodeContainerTerminalsConnected : EventStoreBaseEvent
    {
        public Guid ConnectionId { get; }
        public Guid NodeContainerId { get; }
        public Guid FromTerminalEquipmentId { get; }
        public Guid FromTerminalId { get; }
        public Guid ToTerminalEquipmentId { get; }
        public Guid ToTerminalId { get; }
        public double FiberCoordLength { get; }

        public NodeContainerTerminalsConnected(Guid connectionId, Guid nodeContainerId, Guid fromTerminalEquipmentId, Guid fromTerminalId, Guid toTerminalEquipmentId, Guid toTerminalId, double fiberCoordLength)
        {
            ConnectionId = connectionId;
            NodeContainerId = nodeContainerId;
            FromTerminalEquipmentId = fromTerminalEquipmentId;
            FromTerminalId = fromTerminalId;
            ToTerminalEquipmentId = toTerminalEquipmentId;
            ToTerminalId = toTerminalId;
            FiberCoordLength = fiberCoordLength;
        }
    }


}
