using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events
{
    public record SpanSegmentsConnectedToSimpleTerminals : EventStoreBaseEvent
    {
        public Guid SpanEquipmentId { get; }
        public SpanSegmentToSimpleTerminalConnectInfo[] Connects { get; }

        public SpanSegmentsConnectedToSimpleTerminals(Guid spanEquipmentId, SpanSegmentToSimpleTerminalConnectInfo[] connects)
        {
            SpanEquipmentId = spanEquipmentId;
            Connects = connects;
        }
    }

    public record SpanSegmentToSimpleTerminalConnectInfo
    {
        public Guid SegmentId { get; }
        public Guid TerminalId { get; set; }
        public SpanSegmentToTerminalConnectionDirection ConnectionDirection { get; set; }
   
        public SpanSegmentToSimpleTerminalConnectInfo(Guid segmentId, Guid terminalId)
        {
            SegmentId = segmentId;
            TerminalId = terminalId;
        }
    }

    public enum SpanSegmentToTerminalConnectionDirection
    {
        FromSpanSegmentToTerminal,
        FromTerminalToSpanSegment
    }
}
