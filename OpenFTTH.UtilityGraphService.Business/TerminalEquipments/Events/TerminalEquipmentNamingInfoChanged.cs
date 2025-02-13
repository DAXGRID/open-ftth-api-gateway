using OpenFTTH.Events;
using OpenFTTH.Events.Core.Infos;
using System;

namespace OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Events
{
    public record TerminalEquipmentNamingInfoChanged : EventStoreBaseEvent
    {
        public Guid TerminalEquipmentId { get; }
        public NamingInfo? NamingInfo { get; }

        public TerminalEquipmentNamingInfoChanged(Guid terminalEquipmentId, NamingInfo? namingInfo)
        {
            TerminalEquipmentId = terminalEquipmentId;
            NamingInfo = namingInfo;
        }
    }
}
