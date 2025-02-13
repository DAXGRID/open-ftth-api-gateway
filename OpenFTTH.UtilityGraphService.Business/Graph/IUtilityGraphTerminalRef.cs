using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.Business.Graph
{
    public interface IUtilityGraphTerminalRef : IUtilityGraphElement
    {
        TerminalEquipment TerminalEquipment(UtilityNetworkProjection utilityNetwork);
        TerminalStructure TerminalStructure(UtilityNetworkProjection utilityNetwork);
        Terminal Terminal(UtilityNetworkProjection utilityNetwork);
        Guid RouteNodeId { get; }
        Guid TerminalEquipmentId { get; }
        Guid TerminalId { get; }
        ushort StructureIndex { get; }
        ushort TerminalIndex { get; }

        bool IsDummyEnd { get; }
    }
}
