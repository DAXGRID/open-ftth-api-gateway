namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views
{
    public record SpanEquipmentAZConnectivityViewEndInfo
    {
        public string? ConnectedTo { get; init; }
        public string? End { get; init; }

        public SpanEquipmentAZConnectivityViewEndInfo()
        {
        }
    }
}
