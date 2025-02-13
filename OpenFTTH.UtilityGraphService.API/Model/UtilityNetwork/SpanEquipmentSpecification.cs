using OpenFTTH.Core;
using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public record SpanEquipmentSpecification : IIdentifiedObject
    {
        public Guid Id { get; }
        public string Category { get;}
        public string Name { get; }
        public SpanStructureTemplate RootTemplate { get; }
        public bool Deprecated { get; init; }
        public bool IsFixed { get; init; }
        public bool IsMultiLevel { get; init; }
        public string? Description { get; init; }
        public Guid[]? ManufacturerRefs { get; init; }
        public bool IsCable { get; init; }

        /// <summary>
        /// </summary>
        /// <param name="id">The specification id</param>
        /// <param name="category">What kind of category: Conduit, Fiber Cable etc.</param>
        /// <param name="name">Short human readable name of the specification - i.e. Ø50 12x10</param>
        /// <param name="version">Since specifications are immutable, a version must always be provided</param>
        public SpanEquipmentSpecification(Guid id, string category, string name, SpanStructureTemplate rootTemplate)
        {
            this.Id = id;
            this.Category = category;
            this.Name = name;
            this.RootTemplate = rootTemplate;
        }
    }
}

