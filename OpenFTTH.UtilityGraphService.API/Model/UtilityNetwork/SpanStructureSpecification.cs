using OpenFTTH.Core;
using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    /// <summary>
    /// Used as part of a span equipment specification to specify color and dimension of physical conduits, fiber strings etc.
    /// </summary>
    public record SpanStructureSpecification : IIdentifiedObject
    {
        public Guid Id { get;}
        public string SpanClassType { get; }
        public string Name { get; }
        public string Color { get; }
        public int? InnerDiameter { get; init; }
        public int? OuterDiameter { get; init; }
        public bool Deprecated { get; init; }
        public string? Description { get; init; }

        public SpanStructureSpecification(Guid id, string spanClassType, string name, string color)
        {
            this.Id = id;
            this.SpanClassType = spanClassType;
            this.Name = name;
            this.Color = color;
        }
    }
}
