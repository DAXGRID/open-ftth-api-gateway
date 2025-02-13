using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public record SpanSegmentWithIndexInfo
    {
        public SpanSegment SpanSegment { get; }
        public UInt16 StructureIndex { get; }
        public UInt16 SegmentIndex { get; }

        public SpanSegmentWithIndexInfo(SpanSegment spanSegment, ushort structureIndex, ushort segmentIndex)
        {
            SpanSegment = spanSegment;
            StructureIndex = structureIndex;
            SegmentIndex = segmentIndex;
        }
    }
}
