using OpenFTTH.Events;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events
{
    public record ManufacturerAdded : EventStoreBaseEvent
    {
        public Manufacturer Manufacturer { get; }

        public ManufacturerAdded(Manufacturer manufacturer)
        {
            Manufacturer = manufacturer;
        }
    }
}
