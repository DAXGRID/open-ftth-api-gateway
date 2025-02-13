using System.Threading.Tasks;
using OpenFTTH.EventSourcing;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Events;

namespace OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Projections
{
    public class TerminalEquipmentSpecificationsProjection : ProjectionBase
    {
        private readonly LookupCollection<TerminalEquipmentSpecification> _terminalEquipmentSpecifications = new();

        public LookupCollection<TerminalEquipmentSpecification> Specifications => _terminalEquipmentSpecifications;

        public TerminalEquipmentSpecificationsProjection()
        {
            ProjectEventAsync<TerminalEquipmentSpecificationAdded>(ProjectAsync);
        }

        private Task ProjectAsync(IEventEnvelope eventEnvelope)
        {
            switch (eventEnvelope.Data)
            {
                case (TerminalEquipmentSpecificationAdded @event):
                    _terminalEquipmentSpecifications.Add(@event.Specification);
                    break;
            }

            return Task.CompletedTask;
        }
    }
}
