using OpenFTTH.Events;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Events
{
    public record TagsUpdated : EventStoreBaseEvent
    {
        public Guid TerminalOrSpanEquipmentId { get; }
        public EquipmentTag[]? Tags { get; }

        public TagsUpdated(Guid terminalOrSpanEquipmentId, EquipmentTag[]? tags)
        {
            TerminalOrSpanEquipmentId = terminalOrSpanEquipmentId;
            Tags = tags;
        }
    }
}
