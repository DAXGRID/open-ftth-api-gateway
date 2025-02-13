using System.Threading.Tasks;
using OpenFTTH.EventSourcing;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers.Projections
{
    public class NodeContainerSpecificationsProjection : ProjectionBase
    {
        private readonly LookupCollection<NodeContainerSpecification> _nodeContainerSpecifications = new LookupCollection<NodeContainerSpecification>();

        public LookupCollection<NodeContainerSpecification> Specifications => _nodeContainerSpecifications;

        public NodeContainerSpecificationsProjection()
        {
            ProjectEventAsync<NodeContainerSpecificationAdded>(ProjectAsync);
        }

        private Task ProjectAsync(IEventEnvelope eventEnvelope)
        {
            switch (eventEnvelope.Data)
            {
                case (NodeContainerSpecificationAdded @event):
                    _nodeContainerSpecifications.Add(@event.Specification);
                    break;
            }

            return Task.CompletedTask;
        }
    }
}
