using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events
{
    public record SpanEquipmentCutReverted : EventStoreBaseEvent
    {
        public Guid SpanEquipmentId { get; }
        public Guid CutNodeOfInterestId { get; }
        public SpanSegmentCutRevertInfo[] Reverts { get; }

        public SpanEquipmentCutReverted(Guid spanEquipmentId, Guid cutNodeOfInterestId, SpanSegmentCutRevertInfo[] reverts)
        {
            SpanEquipmentId = spanEquipmentId;
            CutNodeOfInterestId = cutNodeOfInterestId;
            Reverts = reverts;
        }
    }

    public record SpanSegmentCutRevertInfo
    {
        public Guid OldSpanSegmentId1 { get; }
        public Guid OldSpanSegmentId2 { get; }
        public Guid NewSpanSegmentId { get; }

        public SpanSegmentCutRevertInfo(Guid oldSpanSegmentId1, Guid oldSpanSegmentId2, Guid newSpanSegmentId)
        {
            OldSpanSegmentId1 = oldSpanSegmentId1;
            OldSpanSegmentId2 = oldSpanSegmentId2;
            NewSpanSegmentId = newSpanSegmentId;
        }
    }
}
