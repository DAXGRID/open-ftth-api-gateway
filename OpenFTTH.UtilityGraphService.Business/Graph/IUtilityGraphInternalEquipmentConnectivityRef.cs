using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.Business.Graph
{
    public interface IUtilityInternalEquipmentConnectivityRef : IUtilityGraphElement
    {
        TerminalEquipment TerminalEquipment(UtilityNetworkProjection utilityNetwork);
        TerminalStructure TerminalStructure(UtilityNetworkProjection utilityNetwork);
        Guid RouteNodeId { get; }
        Guid TerminalEquipmentId { get; }
        ushort StructureIndex { get; }
    }
}
