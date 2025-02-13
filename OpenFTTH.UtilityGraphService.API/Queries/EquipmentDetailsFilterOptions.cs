namespace OpenFTTH.UtilityGraphService.API.Queries
{
    public record EquipmentDetailsFilterOptions
    {
        public bool IncludeRouteNetworkTrace { get; init; }

        public EquipmentDetailsFilterOptions()
        {
        }
    }
}
