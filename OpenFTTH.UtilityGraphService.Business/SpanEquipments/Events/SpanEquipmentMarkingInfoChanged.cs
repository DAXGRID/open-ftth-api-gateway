using OpenFTTH.Events;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events
{
    public record SpanEquipmentMarkingInfoChanged : EventStoreBaseEvent
    {
        public Guid SpanEquipmentId { get; }

        public MarkingInfo? MarkingInfo { get; }

        public SpanEquipmentMarkingInfoChanged(Guid spanEquipmentId, MarkingInfo? markingInfo)
        {
            SpanEquipmentId = spanEquipmentId;
            MarkingInfo = markingInfo;
        }
    }
}
