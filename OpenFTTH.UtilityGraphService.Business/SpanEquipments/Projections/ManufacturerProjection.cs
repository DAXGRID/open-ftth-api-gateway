using System.Threading.Tasks;
using OpenFTTH.EventSourcing;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections
{
    public class ManufacturerProjection : ProjectionBase
    {
        private readonly LookupCollection<Manufacturer> _manufacturer = new LookupCollection<Manufacturer>();

        public LookupCollection<Manufacturer> Manufacturer => _manufacturer;

        public ManufacturerProjection()
        {
            ProjectEventAsync<ManufacturerAdded>(ProjectAsync);
        }

        private Task ProjectAsync(IEventEnvelope eventEnvelope)
        {
            switch (eventEnvelope.Data)
            {
                case (ManufacturerAdded @event):
                    _manufacturer.Add(@event.Manufacturer);
                    break;
            }

            return Task.CompletedTask;
        }
    }
}
