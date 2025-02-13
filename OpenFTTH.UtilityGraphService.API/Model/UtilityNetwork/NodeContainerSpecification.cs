using OpenFTTH.Core;
using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public record NodeContainerSpecification : IIdentifiedObject
    {
        public Guid Id { get; }
        public string Category { get;}
        public string Name { get; }
        public bool Deprecated { get; init; }

        public string? Description { get; init; }

        public Guid[]? ManufacturerRefs { get; init; }

        /// <summary>
        /// </summary>
        /// <param name="id">The specification id</param>
        /// <param name="category">What kind of category: Man Holes, Cabinets, Conduit Closures etc.</param>
        /// <param name="name">Short human readable name of the specification - i.e. Well deluxe bla bla</param>
        public NodeContainerSpecification(Guid id, string category, string name)
        {
            this.Id = id;
            this.Category = category;
            this.Name = name;
        }
    }
}

