using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Events
{
    public record TerminalEquipmentManufacturerChanged : EventStoreBaseEvent
    {
        public Guid TerminalEquipmentId { get; }
        public Guid ManufacturerId { get; }

        public TerminalEquipmentManufacturerChanged(Guid terminalEquipmentId, Guid manufacturerId)
        {
            TerminalEquipmentId = terminalEquipmentId;
            ManufacturerId = manufacturerId;
        }
    }
}
