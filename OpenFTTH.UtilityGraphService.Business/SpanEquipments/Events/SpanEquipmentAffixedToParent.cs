using OpenFTTH.Events;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events
{
    public record SpanEquipmentAffixedToParent : EventStoreBaseEvent
    {
        public Guid SpanEquipmentId { get; }
        public UtilityNetworkHop[] NewUtilityHopList { get; init; }

        public SpanEquipmentAffixedToParent(Guid spanEquipmentId, UtilityNetworkHop[] newUtilityHopList)
        {
            SpanEquipmentId = spanEquipmentId;
            NewUtilityHopList = newUtilityHopList;
        }
    }
}
