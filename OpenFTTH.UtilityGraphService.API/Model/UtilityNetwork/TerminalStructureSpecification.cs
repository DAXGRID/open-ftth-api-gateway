using OpenFTTH.Core;
using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    /// <summary>
    /// Used as part of a terminal equipment for specification specifying cards, trays etc.
    /// </summary>
    public record TerminalStructureSpecification : IIdentifiedObject
    {
        public Guid Id { get;}
        public string Category { get; }
        public string Name { get; }
        public string ShortName { get; }   
        public TerminalTemplate[] TerminalTemplates { get; }
        public bool Deprecated { get; init; }
        public string? Description { get; init; }
        public Guid[]? ManufacturerRefs { get; init; }
        public bool IsCustomerSplitter { get; init; }
        public bool IsInterfaceModule { get; init; }

        public TerminalStructureSpecification(Guid id, string category, string name, string shortName, TerminalTemplate[] terminalTemplates)
        {
            Id = id;
            Category = category;
            Name = name;
            ShortName = shortName;
            TerminalTemplates = terminalTemplates;
        }
    }
}
