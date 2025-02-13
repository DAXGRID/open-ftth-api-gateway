using OpenFTTH.Events;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers.Events
{
    public record SpanEquipmentDetachedFromParent : EventStoreBaseEvent
    {
        public Guid SpanEquipmentId { get; }
        public UtilityNetworkHop[] NewUtilityHopList { get; init; }

        public SpanEquipmentDetachedFromParent(Guid spanEquipmentId, UtilityNetworkHop[] newUtilityHopList)
        {
            SpanEquipmentId = spanEquipmentId;
            NewUtilityHopList = newUtilityHopList;
        }
    }
}
