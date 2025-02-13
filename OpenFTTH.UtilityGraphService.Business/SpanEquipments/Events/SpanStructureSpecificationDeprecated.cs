using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events
{
    public record SpanStructureSpecificationDeprecated : EventStoreBaseEvent
    {
        public Guid SpanStructureSpecificationId { get; }

        public SpanStructureSpecificationDeprecated(Guid spanStructureSpecificationId)
        {
            SpanStructureSpecificationId = spanStructureSpecificationId;
        }
    }
}
