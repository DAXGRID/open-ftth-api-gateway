using System;

namespace OpenFTTH.UtilityGraphService.API.Model.Asset 
{ 
    public class Manufacturer : IManufacturer
    {
        public Guid Id { get; }
        public string Name { get; }
        public bool Deprecated { get; init; }
        public string? Description { get; init; }

        public Manufacturer(Guid id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
