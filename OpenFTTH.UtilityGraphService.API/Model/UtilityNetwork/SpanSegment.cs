using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public record SpanSegment
    {
        public Guid Id { get; }
        public UInt16 FromNodeOfInterestIndex { get; init; }
        public UInt16 ToNodeOfInterestIndex { get; init; }
        public Guid FromTerminalId { get; init; }
        public Guid ToTerminalId { get; init; }

        public SpanSegment(Guid id, UInt16 fromNodeOfInterestIndex, UInt16 toNodeOfInterestIndex)
        {
            Id = id;
            FromNodeOfInterestIndex = fromNodeOfInterestIndex;
            ToNodeOfInterestIndex = toNodeOfInterestIndex;
        }
    }
}
