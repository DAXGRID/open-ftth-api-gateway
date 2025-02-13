using OpenFTTH.Events;
using OpenFTTH.Events.Core.Infos;
using System;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events
{
    public record SpanEquipmentAddressInfoChanged : EventStoreBaseEvent
    {
        public Guid SpanEquipmentId { get; }

        public AddressInfo? AddressInfo { get; }

        public SpanEquipmentAddressInfoChanged(Guid spanEquipmentId, AddressInfo? addressInfo)
        {
            SpanEquipmentId = spanEquipmentId;
            AddressInfo = addressInfo;
        }
    }
}
