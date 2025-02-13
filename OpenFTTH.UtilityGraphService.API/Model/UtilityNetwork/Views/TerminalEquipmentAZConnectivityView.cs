namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views
{
    /// <summary>
    /// View data model for holding connectivity information for one of more terminal equipment for use in connectivity views, editors etc.
    /// </summary>
    public record TerminalEquipmentAZConnectivityViewModel
    {
        /// <summary>
        /// Optionally array of node structures - i.e. racks - that the terminal equipment are placed within
        /// </summary>
        public TerminalEquipmentAZConnectivityViewNodeStructureInfo[]? ParentNodeStructures {get; init;}

        /// <summary>
        /// All terminal equipments to be displayed
        /// </summary>
        public TerminalEquipmentAZConnectivityViewEquipmentInfo[] TerminalEquipments { get; init; }

        public TerminalEquipmentAZConnectivityViewModel(TerminalEquipmentAZConnectivityViewEquipmentInfo[] terminalEquipments)
        {
            TerminalEquipments = terminalEquipments;
        }
    }
}
