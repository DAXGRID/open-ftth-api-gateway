using OpenFTTH.Events;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events
{
    public record SpanEquipmentPlacedInRouteNetwork : EventStoreBaseEvent
    {
        public SpanEquipment Equipment { get; }

        public SpanEquipmentPlacedInRouteNetwork(SpanEquipment equipment)
        {
            this.Equipment = equipment;
        }
    }
}
