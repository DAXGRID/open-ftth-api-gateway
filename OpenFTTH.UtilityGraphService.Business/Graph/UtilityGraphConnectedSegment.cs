using DAX.ObjectVersioning.Graph;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.Business.Graph
{
    public class UtilityGraphConnectedSegment : GraphEdge, IUtilityGraphSegmentRef
    {
        public Guid SpanEquipmentId { get; }
        public ushort StructureIndex { get; }
        public ushort SegmentIndex { get; }
        public bool IsPatch { get; set; }

        public UtilityGraphConnectedSegment(Guid id, GraphNode? fromNode, GraphNode? toNode, Guid spanEquipmentId, UInt16 structureIndex, UInt16 segmentIndex) : base(id, fromNode, toNode)
        {
            SpanEquipmentId = spanEquipmentId;
            StructureIndex = structureIndex;
            SegmentIndex = segmentIndex;
        }

        public UtilityGraphConnectedSegment(Guid id, Guid spanEquipmentId, UInt16 structureIndex, UInt16 segmentIndex) : base(id, null, null)
        {
            SpanEquipmentId = spanEquipmentId;
            StructureIndex = structureIndex;
            SegmentIndex = segmentIndex;
        }

        public IUtilityGraphSegmentRef CreateWithNewSegmentIndex(ushort newSegmentIndex)
        {
            var newSegment = new UtilityGraphConnectedSegment(Id, SpanEquipmentId, StructureIndex, newSegmentIndex);
            CopyNodeRelationshipsTo(newSegment);
            return newSegment;
        }

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

        public override string ToString()
        {
            return $"SpanEquipment: {SpanEquipmentId} StructureIndex: {StructureIndex} SpanIndex: {SegmentIndex}";
        }

    }
}
