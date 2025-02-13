using OpenFTTH.Events;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;

namespace OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Events
{
    public record TerminalEquipmentSpecificationAdded : EventStoreBaseEvent
    {
        public TerminalEquipmentSpecification Specification { get; }

        public TerminalEquipmentSpecificationAdded(TerminalEquipmentSpecification specification)
        {
            Specification = specification;
        }
    }
}
