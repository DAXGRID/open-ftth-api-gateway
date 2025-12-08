using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public record EquipmentTag
    {
        public Guid TerminalOrSpanId { get; }
        public string[]? Tags { get; }
        public string? Comment { get; }

        public EquipmentTag(Guid terminalOrSpanId, string[] tags, string? comment = null)
        {
            TerminalOrSpanId = terminalOrSpanId;
            Tags = tags;
            Comment = comment;
        }
    }

    public record EquipmentDisplayTag
    {
        public Guid TerminalOrSpanId { get; }
        public string DisplayName { get; }
        public string[]? Tags { get; }
        public string? Comment { get; }

        public EquipmentDisplayTag(Guid terminalOrSpanId, string displayName, string[] tags, string? comment = null)
        {
            TerminalOrSpanId = terminalOrSpanId;
            DisplayName = displayName;
            Tags = tags;
            Comment = comment;
        }
    }
}
