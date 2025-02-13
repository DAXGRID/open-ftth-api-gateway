using OpenFTTH.Events.Core.Infos;
using System;
using System.Collections.Immutable;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public record SpanEquipment : IEquipment
    {
        public Guid Id { get; }
        public Guid WalkOfInterestId { get; }
        public Guid SpecificationId { get; init; }
        public Guid[] NodesOfInterestIds { get; init; }
        public SpanStructure[] SpanStructures { get; init; }

        public Guid? ManufacturerId { get; init; }
        public NamingInfo? NamingInfo { get; init; }
        public LifecycleInfo? LifecycleInfo { get; init; }
        public MarkingInfo? MarkingInfo { get; init; }
        public AddressInfo? AddressInfo { get; init; }
        public bool IsCable { get; init; }

        public SpanEquipmentNodeContainerAffix[]? NodeContainerAffixes { get; init; }

        public UtilityNetworkHop[]? UtilityNetworkHops { get; init; }

        public string? Name => NamingInfo?.Name;
        public string? Description => NamingInfo?.Description;

        public SpanEquipment(Guid id, Guid specificationId, Guid walkOfInterestId, Guid[] nodesOfInterestIds, SpanStructure[] spanStructures)
        {
            this.Id = id;
            this.SpecificationId = specificationId;
            this.WalkOfInterestId = walkOfInterestId;
            this.NodesOfInterestIds = nodesOfInterestIds;
            this.SpanStructures = spanStructures;
        }

        public bool TryGetSpanSegment(Guid spanSegmentId, out SpanSegmentWithIndexInfo spanSegmentwithIndexInfo)
        {
            for (UInt16 structureIndex = 0; structureIndex < SpanStructures.Length; structureIndex++)
            {
                var spanSegments = SpanStructures[structureIndex].SpanSegments;

                for (UInt16 segmentIndex = 0; segmentIndex < spanSegments.Length; segmentIndex++)
                {
                    var spanSegment = spanSegments[segmentIndex];
                    if (spanSegment.Id == spanSegmentId)
                    {
                        spanSegmentwithIndexInfo = new SpanSegmentWithIndexInfo(spanSegment, structureIndex, segmentIndex);
                        return true;
                    }
                }
            }

            #pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            spanSegmentwithIndexInfo = default;
            #pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

            return false;
        }
    }
}
