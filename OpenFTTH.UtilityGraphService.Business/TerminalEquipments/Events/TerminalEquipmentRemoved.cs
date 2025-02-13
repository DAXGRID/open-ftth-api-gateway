using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Events
{
    public record TerminalEquipmentRemoved : EventStoreBaseEvent
    {
        public Guid TerminalEquipmentId { get; }

        public TerminalEquipmentRemoved(Guid terminalEquipmentId)
        {
            TerminalEquipmentId = terminalEquipmentId;
        }
    }
}
