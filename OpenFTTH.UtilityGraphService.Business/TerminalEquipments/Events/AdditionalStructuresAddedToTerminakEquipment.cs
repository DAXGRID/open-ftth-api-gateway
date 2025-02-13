using OpenFTTH.Events;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Events
{
    public record AdditionalStructuresAddedToTerminalEquipment : EventStoreBaseEvent
    {
        public Guid TerminalEquipmentId { get; }
        public TerminalStructure[] TerminalStructuresToAdd {get; }

        public AdditionalStructuresAddedToTerminalEquipment(Guid terminalEquipmentId, TerminalStructure[] terminalStructuresToAdd)
        {
            TerminalEquipmentId = terminalEquipmentId;
            TerminalStructuresToAdd = terminalStructuresToAdd;
        }
    }
}
