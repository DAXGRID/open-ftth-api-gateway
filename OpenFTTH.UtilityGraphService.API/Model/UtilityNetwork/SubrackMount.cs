using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public record SubrackMount
    {
        public Guid TerminalEquipmentId { get; }
        public int Position { get; init; }
        public int HeightInUnits { get; }

        public SubrackMount(Guid terminalEquipmentId, int position, int heightInUnits)
        {
            TerminalEquipmentId = terminalEquipmentId;
            Position = position;
            HeightInUnits = heightInUnits;
        }
    }
}
