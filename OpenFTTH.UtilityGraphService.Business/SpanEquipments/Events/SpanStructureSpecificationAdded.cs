using OpenFTTH.Events;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events
{
    public record SpanStructureSpecificationAdded : EventStoreBaseEvent
    {
        public SpanStructureSpecification Specification { get; }

        public SpanStructureSpecificationAdded(SpanStructureSpecification specification)
        {
            Specification = specification;
        }
    }
}
