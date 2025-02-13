using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events
{
    public record SpanSegmentsCut : EventStoreBaseEvent
    {
        public Guid SpanEquipmentId { get; }
        public Guid CutNodeOfInterestId { get; }
        public UInt16 CutNodeOfInterestIndex { get; }
        public SpanSegmentCutInfo[] Cuts { get; }

        public SpanSegmentsCut(Guid spanEquipmentId, Guid cutNodeOfInterestId, UInt16 cutNodeOfInterestIndex, SpanSegmentCutInfo[] cuts)
        {
            SpanEquipmentId = spanEquipmentId;
            CutNodeOfInterestId = cutNodeOfInterestId;
            CutNodeOfInterestIndex = cutNodeOfInterestIndex;
            Cuts = cuts;
        }
    }

    public record SpanSegmentCutInfo
    {
        public Guid OldSpanSegmentId { get; }
        public Guid NewSpanSegmentId1 { get; }
        public Guid NewSpanSegmentId2 { get; }

        public SpanSegmentCutInfo(Guid oldSpanSegmentId, Guid newSpanSegmentId1, Guid newSpanSegmentId2)
        {
            OldSpanSegmentId = oldSpanSegmentId;
            NewSpanSegmentId1 = newSpanSegmentId1;
            NewSpanSegmentId2 = newSpanSegmentId2;
        }
    }
}
