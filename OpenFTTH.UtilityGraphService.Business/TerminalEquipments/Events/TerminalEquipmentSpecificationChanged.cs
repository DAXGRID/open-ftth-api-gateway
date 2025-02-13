using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Events
{
    public record TerminalEquipmentSpecificationChanged : EventStoreBaseEvent
    {
        public Guid TerminalEquipmentId { get; }
        public Guid NewSpecificationId { get; }

        public TerminalEquipmentSpecificationChanged(Guid terminalEquipmentId, Guid newSpecificationId)
        {
            TerminalEquipmentId = terminalEquipmentId;
            NewSpecificationId = newSpecificationId;
        }
    }
}
