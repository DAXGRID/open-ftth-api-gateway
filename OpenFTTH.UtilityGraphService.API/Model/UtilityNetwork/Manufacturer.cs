using OpenFTTH.Core;
using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public record Manufacturer : IIdentifiedObject
    {
        public Guid Id { get;}
        public string Name { get; }
        public bool Deprecated { get; init; }
        public string? Description { get; init; }

        public Manufacturer(Guid id, string name)
        {
            this.Id = id;
            this.Name = name;
        }
    }
}
