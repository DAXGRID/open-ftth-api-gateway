namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views
{
    /// <summary>
    /// Disconnect span equipment from terminal equipment view 
    /// </summary>
    public record DisconnectSpanEquipmentFromTerminalView
    {
        public string SpanEquipmentName { get; }
        public DisconnectSpanEquipmentFromTerminalViewConnection[] Lines { get; }
        public DisconnectSpanEquipmentFromTerminalView(string spanEquipmentName, DisconnectSpanEquipmentFromTerminalViewConnection[] lines)
        {
            SpanEquipmentName = spanEquipmentName;
            Lines = lines;
        }
    }
}
