using OpenFTTH.Events;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;

namespace OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Events
{
    public record TerminalEquipmentPlacedInNodeContainer : EventStoreBaseEvent
    {
        public TerminalEquipment Equipment { get; }

        public TerminalEquipmentPlacedInNodeContainer(TerminalEquipment equipment)
        {
            this.Equipment = equipment;
        }
    }
}
