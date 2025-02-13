using OpenFTTH.Events;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events
{
    public record SpanEquipmentSpecificationAdded : EventStoreBaseEvent
    {
        public SpanEquipmentSpecification Specification { get; }

        public SpanEquipmentSpecificationAdded(SpanEquipmentSpecification specification)
        {
            Specification = specification;
        }
    }
}
