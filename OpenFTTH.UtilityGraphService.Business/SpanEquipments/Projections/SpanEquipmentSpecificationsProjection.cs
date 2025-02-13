using OpenFTTH.EventSourcing;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections
{
    public class SpanEquipmentSpecificationsProjection : ProjectionBase
    {
        private readonly LookupCollection<SpanEquipmentSpecification> _spanEquipmentSpecifications = new LookupCollection<SpanEquipmentSpecification>();

        public LookupCollection<SpanEquipmentSpecification> Specifications => _spanEquipmentSpecifications;

        public SpanEquipmentSpecificationsProjection()
        {
            ProjectEventAsync<SpanEquipmentSpecificationAdded>(ProjectAsync);
            ProjectEventAsync<SpanEquipmentSpecificationDeprecated>(ProjectAsync);
        }

        private Task ProjectAsync(IEventEnvelope eventEnvelope)
        {
            switch (eventEnvelope.Data)
            {
                case (SpanEquipmentSpecificationAdded @event):
                    _spanEquipmentSpecifications.Add(@event.Specification);
                    break;

                case (SpanEquipmentSpecificationDeprecated @event):
                    _spanEquipmentSpecifications[@event.SpanEquipmentSpecificationId] = _spanEquipmentSpecifications[@event.SpanEquipmentSpecificationId] with { Deprecated = true };
                    break;
            }

            return Task.CompletedTask;
        }
    }
}
