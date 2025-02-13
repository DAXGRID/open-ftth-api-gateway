using OpenFTTH.Events;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Events
{
    public record TerminalStructureInterfaceInfoChanged : EventStoreBaseEvent
    {
        public Guid TerminalEquipmentId { get; }
        public Guid TerminalStructureId { get; }
        public InterfaceInfo? InterfaceInfo { get; }

        public TerminalStructureInterfaceInfoChanged(Guid terminalEquipmentId, Guid terminalStructureId, InterfaceInfo? interfaceInfo)
        {
            TerminalEquipmentId = terminalEquipmentId;
            TerminalStructureId = terminalStructureId;
            InterfaceInfo = interfaceInfo;
        }
    }
}
