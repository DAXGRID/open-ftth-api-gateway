using OpenFTTH.Events;
using OpenFTTH.Events.Core.Infos;
using System;

namespace OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Events
{
    public record TerminalEquipmentAddressInfoChanged : EventStoreBaseEvent
    {
        public Guid TerminalEquipmentId { get; }
        public AddressInfo? AddressInfo { get; }

        public TerminalEquipmentAddressInfoChanged(Guid terminalEquipmentId, AddressInfo? addressInfo)
        {
            TerminalEquipmentId = terminalEquipmentId;
            AddressInfo = addressInfo;
        }
    }
}
