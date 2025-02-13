using OpenFTTH.Events;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers.Events
{
    public record RackSpecificationAdded : EventStoreBaseEvent
    {
        public RackSpecification Specification { get; }

        public RackSpecificationAdded(RackSpecification specification)
        {
            Specification = specification;
        }
    }
}
