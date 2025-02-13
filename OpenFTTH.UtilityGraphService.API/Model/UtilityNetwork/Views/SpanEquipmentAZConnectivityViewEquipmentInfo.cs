using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views
{
    /// <summary>
    /// Span equipment connectiviy view - i.e. some cable or conduit
    /// </summary>
    public record SpanEquipmentAZConnectivityViewEquipmentInfo
    {
        public Guid Id { get; init; }
        public string Category { get; init; }
        public string Name { get; init; }
        public bool IsCable { get; init; }
        public string SpecName { get; init; }
        public string? Info { get; init; }

        public SpanEquipmentAZConnectivityViewLineInfo[] Lines { get; init; }

        public SpanEquipmentAZConnectivityViewEquipmentInfo(Guid id, string category, string name, bool isCable, string specName, SpanEquipmentAZConnectivityViewLineInfo[] lines)
        {
            Id = id;
            Category = category;
            Name = name;
            IsCable = isCable;
            SpecName = specName;
            Lines = lines;
        }
    }
}
