using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events
{
    public record SpanSegmentsDisconnectedFromTerminals : EventStoreBaseEvent
    {
        public Guid SpanEquipmentId { get; }

        public SpanSegmentToTerminalDisconnectInfo[] Disconnects { get; }

        public SpanSegmentsDisconnectedFromTerminals(Guid spanEquipmentId, SpanSegmentToTerminalDisconnectInfo[] disconnects)
        {
            SpanEquipmentId = spanEquipmentId;
            Disconnects = disconnects;
        }
    }

    public record SpanSegmentToTerminalDisconnectInfo
    {
        public Guid SegmentId { get; }
        public Guid TerminalId { get; set; }

        public SpanSegmentToTerminalDisconnectInfo(Guid segmentId, Guid terminalId)
        {
            SegmentId = segmentId;
            TerminalId = terminalId;
        }
    }
}
