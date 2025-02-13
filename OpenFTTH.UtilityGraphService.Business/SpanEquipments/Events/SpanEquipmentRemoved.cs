using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events
{
    public record SpanEquipmentRemoved : EventStoreBaseEvent
    {
        public Guid SpanEquipmentId { get; }

        public SpanEquipmentRemoved(Guid spanEquipmentId)
        {
            SpanEquipmentId = spanEquipmentId;
        }
    }
}
