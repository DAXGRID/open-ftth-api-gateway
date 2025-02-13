using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views
{
    /// <summary>
    /// Span equipment passage view - i.e. some cable or conduit
    /// </summary>
    public record SpanEquipmentPassageViewEquipmentInfo
    {
        public Guid Id { get; init; }
        public string Category { get; init; }
        public string? Name { get; init; }
        public string SpecName { get; init; }
        public string? Info { get; init; }

        public SpanEquipmentPassageViewLineInfo[] Lines { get; init; }

        public SpanEquipmentPassageViewEquipmentInfo(Guid id, string category, string? name, string specName, SpanEquipmentPassageViewLineInfo[] lines)
        {
            Id = id;
            Category = category;
            Name = name;
            SpecName = specName;
            Lines = lines;
        }
    }
}
