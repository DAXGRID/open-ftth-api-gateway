using OpenFTTH.Core;
using System;
using System.Collections.Immutable;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    /// <summary>
    /// Immutable structure holding a span structure and its segments. 
    /// Please keep this structure as light as possible, as millions of these guys will be exchanged and cached in memory.
    /// </summary>
    public record SpanStructure : IIdentifiedObject
    {
        public Guid Id { get; }
        public Guid SpecificationId { get; init; }
        public UInt16 Level { get; }
        public UInt16 ParentPosition { get; }
        public UInt16 Position { get; }
        public SpanSegment[] SpanSegments { get; init; }
        public bool Deleted { get; init; }

        public string? Name => this.GetType().Name;
        public string? Description => null;

        public SpanStructure(Guid id, Guid specificationId, UInt16 level, UInt16 parentPosition, UInt16 position, SpanSegment[] spanSegments)
        {
            this.Id = id;
            this.SpecificationId = specificationId;
            this.Level = level;
            this.ParentPosition = parentPosition;
            this.Position = position;
            this.SpanSegments = spanSegments;
        }
    }
}
