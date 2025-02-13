using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events
{
    public record SpanEquipmentMoved : EventStoreBaseEvent
    {
        public Guid SpanEquipmentId { get; }
        public Guid[] NodesOfInterestIds { get; }

        public SpanEquipmentMoved(Guid spanEquipmentId, Guid[] nodesOfInterestIds)
        {
            SpanEquipmentId = spanEquipmentId;
            NodesOfInterestIds = nodesOfInterestIds;
        }
    }
}
