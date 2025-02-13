using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views
{
    /// <summary>
    /// Terminal equipment - i.e. a some splice closure,  subrack etc.
    /// </summary>
    public record TerminalEquipmentAZConnectivityViewEquipmentInfo
    {
        public Guid Id { get; init; }
        public Guid? ParentNodeStructureId { get; init; }
        public string Category { get; init; }
        public string Name { get; init; }
        public string SpecName { get; init; }
        public string? Info { get; init; }

        public bool IsLineTermination { get; init; }

        public TerminalEquipmentAZConnectivityViewTerminalStructureInfo[] TerminalStructures { get; init; }

        public TerminalEquipmentAZConnectivityViewEquipmentInfo(Guid id, string category, string name, string specName, TerminalEquipmentAZConnectivityViewTerminalStructureInfo[] terminalStructures, bool isLineTermination)
        {
            Id = id;
            Category = category;
            Name = name;
            SpecName = specName;
            TerminalStructures = terminalStructures;
            IsLineTermination = isLineTermination;
        }
    }
}
