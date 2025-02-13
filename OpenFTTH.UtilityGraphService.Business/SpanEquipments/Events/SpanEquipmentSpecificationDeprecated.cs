using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events
{
    public record SpanEquipmentSpecificationDeprecated : EventStoreBaseEvent
    {
        public Guid SpanEquipmentSpecificationId { get; }

        public SpanEquipmentSpecificationDeprecated(Guid spanEquipmentSpecificationId)
        {
            SpanEquipmentSpecificationId = spanEquipmentSpecificationId;
        }
    }
}
