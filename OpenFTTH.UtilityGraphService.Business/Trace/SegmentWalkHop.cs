using OpenFTTH.Events.Core.Infos;
using System;

namespace OpenFTTH.UtilityGraphService.Business.Trace
{
    public record SegmentWalkHop
    {
        public Guid SpanSegmentId { get; }
        public Guid FromNodeId { get; }
        public Guid ToNodeId { get; }
        public Guid WalkOfInterestId { get; }
        public AddressInfo? AddressInfo { get; }

        public SegmentWalkHop(Guid spanEquipmentOrSegmentId, Guid fromNodeId, Guid toNodeId, Guid walkOfInterestId, AddressInfo? addressInfo)
        {
            SpanSegmentId = spanEquipmentOrSegmentId;
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            WalkOfInterestId = walkOfInterestId;
            AddressInfo = addressInfo;
        }
    }
}
