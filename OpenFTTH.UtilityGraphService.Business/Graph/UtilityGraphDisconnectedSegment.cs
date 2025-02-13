using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.Business.Graph
{
    /// <summary>
    /// Used when a span segment is unused - i.e. an inner conduit or fiber is yet to be used/connected.
    /// This to prevent using tons of memory (arround 200+ bytes due to GraphEdge holding two dicts).
    /// So we use this light weight class (that is not derived from GraphEdge) to represent the not 
    /// connected span segments.
    /// </summary>
    public class UtilityGraphDisconnectedSegment : IUtilityGraphSegmentRef
    {
        public Guid SpanEquipmentId { get; }
        public UInt16 StructureIndex { get; }
        public UInt16 SegmentIndex { get; }

        public SpanEquipment SpanEquipment(UtilityNetworkProjection utilityNetwork)
        {
            if (utilityNetwork.TryGetEquipment<SpanEquipment>(SpanEquipmentId, out var spanEquipment))
                return spanEquipment;

            throw new ApplicationException($"Cannot find span equipment with id: {SpanEquipmentId}. State corrupted!");
        }

        public SpanSegment SpanSegment(UtilityNetworkProjection utilityNetwork)
        {
            if (utilityNetwork.TryGetEquipment<SpanEquipment>(SpanEquipmentId, out var spanEquipment))
                return spanEquipment.SpanStructures[StructureIndex].SpanSegments[SegmentIndex];

            throw new ApplicationException($"Cannot find span equipment with id: {SpanEquipmentId}. State corrupted!");
        }

        public IUtilityGraphSegmentRef CreateWithNewSegmentIndex(ushort newSegmentIndex)
        {
            return new UtilityGraphDisconnectedSegment(SpanEquipmentId, StructureIndex, newSegmentIndex);
        }

        public UtilityGraphDisconnectedSegment(Guid spanEquipmentId, ushort structureIndex, ushort segmentIndex)
        {
            SpanEquipmentId = spanEquipmentId;
            StructureIndex = structureIndex;
            SegmentIndex = segmentIndex;
        }

    }
}
