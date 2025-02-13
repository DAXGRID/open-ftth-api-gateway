using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.Business.Graph
{
    /// <summary>
    /// Used when a terminal is unused - i.e. a splice or port is yet to be used/connected.
    /// This to prevent using tons of memory (arround 200+ bytes due to GraphEdge holding two dicts).
    /// So we use this light weight class (that is not derived from GraphEdge) to represent the not 
    /// connected terminals.
    /// </summary>
    public class UtilityGraphDisconnectedTerminal : IUtilityGraphTerminalRef
    {
        public Guid RouteNodeId { get; }
        public Guid TerminalId { get; }
        public Guid TerminalEquipmentId { get; }
        public UInt16 StructureIndex { get; }
        public UInt16 TerminalIndex { get; }
        public bool IsDummyEnd => TerminalId == Guid.Empty;
        public bool IsSimpleTerminal => TerminalEquipmentId == Guid.Empty;

        public UtilityGraphDisconnectedTerminal(Guid routeNodeId, Guid terminalId, Guid terminalEquipmentId, ushort structureIndex, ushort terminalIndex)
        {
            RouteNodeId = routeNodeId;
            TerminalId = terminalId;
            TerminalEquipmentId = terminalEquipmentId;
            StructureIndex = structureIndex;
            TerminalIndex = terminalIndex;
        }

        public TerminalEquipment TerminalEquipment(UtilityNetworkProjection utilityNetwork)
        {
            if (utilityNetwork.TryGetEquipment<TerminalEquipment>(TerminalEquipmentId, out var terminalEquipment))
                return terminalEquipment;

            throw new ApplicationException($"Cannot find terminal equipment with id: {TerminalEquipmentId}. State corrupted!");
        }

        public TerminalStructure TerminalStructure(UtilityNetworkProjection utilityNetwork)
        {
            if (utilityNetwork.TryGetEquipment<TerminalEquipment>(TerminalEquipmentId, out var terminalEquipment))
                return terminalEquipment.TerminalStructures[StructureIndex];

            throw new ApplicationException($"Cannot find terminal equipment with id: {TerminalEquipmentId}. State corrupted!");
        }

        public Terminal Terminal(UtilityNetworkProjection utilityNetwork)
        {
            if (utilityNetwork.TryGetEquipment<TerminalEquipment>(TerminalEquipmentId, out var terminalEquipment))
                return terminalEquipment.TerminalStructures[StructureIndex].Terminals[TerminalIndex];

            throw new ApplicationException($"Cannot find terminial equipment with id: {TerminalEquipmentId}. State corrupted!");
        }
    }
}
