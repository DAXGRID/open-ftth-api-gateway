using OpenFTTH.Events;
using OpenFTTH.Events.Core.Infos;
using System;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events
{
    public record SpanEquipmentNamingInfoChanged : EventStoreBaseEvent
    {
        public Guid SpanEquipmentId { get; }
        public NamingInfo? NamingInfo { get; }

        public SpanEquipmentNamingInfoChanged(Guid spanEquipmentId, NamingInfo? namingInfo)
        {
            SpanEquipmentId = spanEquipmentId;
            NamingInfo = namingInfo;
        }
    }
}
