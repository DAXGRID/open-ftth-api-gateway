using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views
{
    public record DisconnectSpanEquipmentFromTerminalViewConnection
    {
        public Guid TerminalId { get; set; }
        public Guid SegmentId { get; set; }
        public bool IsConnected { get; set; }
        public int SpanStructurePosition { get; }
        public string SpanStructureName { get; }
        public string? TerminalEquipmentName { get; }
        public string? TerminalStructureName { get; }
        public string? TerminalName { get; }
        public string? End { get; }

        public DisconnectSpanEquipmentFromTerminalViewConnection(Guid terminalId, Guid segmentId, bool isConnected, int spanStructurePosition, string spanStructureName, string? terminalEquipmentName, string? terminalStructureName, string? terminalName, string? end)
        {
            TerminalId = terminalId;
            SegmentId = segmentId;
            IsConnected = isConnected;
            SpanStructurePosition = spanStructurePosition;
            SpanStructureName = spanStructureName;
            TerminalEquipmentName = terminalEquipmentName;
            TerminalStructureName = terminalStructureName;
            TerminalName = terminalName;
            End = end;
        }
    }
}
