using OpenFTTH.Events;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;

namespace OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Events
{
    public record TerminalStructureSpecificationAdded : EventStoreBaseEvent
    {
        public TerminalStructureSpecification Specification { get; }

        public TerminalStructureSpecificationAdded(TerminalStructureSpecification specification)
        {
            Specification = specification;
        }
    }
}
