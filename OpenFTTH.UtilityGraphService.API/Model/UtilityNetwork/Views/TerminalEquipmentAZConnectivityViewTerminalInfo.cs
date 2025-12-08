using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views
{
    public record TerminalEquipmentAZConnectivityViewTerminalInfo
    {
        public Guid Id { get; init; }
        public string Name { get; init; }

        public bool HasTags { get; init; }

        public TerminalEquipmentAZConnectivityViewTerminalInfo(Guid id, string name, bool hasTags)
        {
            Id = id;
            Name = name;
            HasTags = hasTags;
        }
    }
}
