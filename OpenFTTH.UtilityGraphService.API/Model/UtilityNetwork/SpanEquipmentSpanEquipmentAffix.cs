using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    /// <summary>
    /// A span equipment affixed to a segment of another span equipment - i.e. a cable placed inside a conduit,
    /// cable tray etc.
    /// </summary>
    public record SpanEquipmentSpanEquipmentAffix
    {
        public Guid SpanSegmentId { get; }
        public SpanEquipmentAffixDirectionEnum Direction { get; }

        public SpanEquipmentSpanEquipmentAffix(Guid spanSegmentId, SpanEquipmentAffixDirectionEnum direction)
        {
            SpanSegmentId = spanSegmentId;
            Direction = direction;
        }
    }
}
