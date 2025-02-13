using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public record Rack
    {
        public Guid Id { get; }
        public string Name { get; init; }
        public int Position { get; init; }
        public Guid SpecificationId { get; init; }
        public int HeightInUnits { get; init; }

        public SubrackMount[] SubrackMounts { get; init; }

        public Rack(Guid id, string name, int position, Guid specificationId, int heightInUnits, SubrackMount[] subrackMounts)
        {
            Id = id;
            Name = name;
            Position = position;
            SpecificationId = specificationId;
            HeightInUnits = heightInUnits;
            SubrackMounts = subrackMounts;
        }
    }
}
