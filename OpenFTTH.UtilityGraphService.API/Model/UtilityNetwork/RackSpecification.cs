using OpenFTTH.Core;
using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public record RackSpecification : IIdentifiedObject
    {
        public Guid Id { get;}
        public string Name { get; }
        public string ShortName { get; }
        public bool Deprecated { get; init; }
        public string? Description { get; init; }

        public RackSpecification(Guid id, string name, string shortName)
        {
            Id = id;
            Name = name;
            ShortName = shortName;
        }
    }
}
