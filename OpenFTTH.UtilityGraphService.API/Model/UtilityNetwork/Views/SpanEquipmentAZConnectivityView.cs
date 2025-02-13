namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views
{
    /// <summary>
    /// View data model for holding connectivity information for one of more span equipments for use in connectivity views, editors etc.
    /// </summary>
    public record SpanEquipmentAZConnectivityViewModel
    {
        /// <summary>
        /// All span equipments to be displayed
        /// </summary>
        public SpanEquipmentAZConnectivityViewEquipmentInfo[] SpanEquipments { get; init; }

        public SpanEquipmentAZConnectivityViewModel(SpanEquipmentAZConnectivityViewEquipmentInfo[] spanEquipments)
        {
            SpanEquipments = spanEquipments;
        }
    }
}
