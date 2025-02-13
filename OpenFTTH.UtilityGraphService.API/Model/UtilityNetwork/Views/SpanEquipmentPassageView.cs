namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views
{
    /// <summary>
    /// View data model for holding information how a span equipments runs through the network
    /// </summary>
    public record SpanEquipmentPassageViewModel
    {
        /// <summary>
        /// All span equipments to be displayed
        /// </summary>
        public SpanEquipmentPassageViewEquipmentInfo[] SpanEquipments { get; init; }

        public SpanEquipmentPassageViewModel(SpanEquipmentPassageViewEquipmentInfo[] spanEquipments)
        {
            SpanEquipments = spanEquipments;
        }
    }
}
