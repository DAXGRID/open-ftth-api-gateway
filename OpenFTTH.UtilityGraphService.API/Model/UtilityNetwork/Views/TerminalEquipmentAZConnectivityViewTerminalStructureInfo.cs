using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views
{
    /// <summary>
    /// Terminal equipment structure (i.e. a splice tray, splitter module or line card)
    /// with properties suitable for a connectivity view
    /// </summary>
    public record TerminalEquipmentAZConnectivityViewTerminalStructureInfo
    {
        public Guid Id { get; init; }
        public string Category { get; init; }
        public string Name { get; init; }
        public string SpecName { get; init; }
        public string? Info { get; init; }
        public bool IsInterfaceModule { get; init; }

        public TerminalEquipmentAZConnectivityViewLineInfo[] Lines { get; init; }

        public TerminalEquipmentAZConnectivityViewTerminalStructureInfo(Guid id, string category, string name, string specName, bool isInterfaceModule, TerminalEquipmentAZConnectivityViewLineInfo[] lines)
        {
            Id = id;
            Category = category;
            Name = name;
            SpecName = specName;
            IsInterfaceModule = isInterfaceModule;
            Lines = lines;
        }
    }
}
