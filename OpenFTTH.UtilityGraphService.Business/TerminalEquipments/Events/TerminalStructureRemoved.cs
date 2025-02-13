using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Events
{
    public record TerminalStructureRemoved : EventStoreBaseEvent
    {
        public Guid TerminalEquipmentId { get; }
        public Guid TerminalStructureId { get; }

        public TerminalStructureRemoved(Guid terminalEquipmentId, Guid terminalStructureId)
        {
            TerminalEquipmentId = terminalEquipmentId;
            TerminalStructureId = terminalStructureId;
        }
    }
}
