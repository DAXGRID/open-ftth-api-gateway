using DAX.ObjectVersioning.Graph;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.Business.Graph
{
    public class UtilityGraphConnectedTerminal : GraphNode, IUtilityGraphTerminalRef
    {
        public Guid RouteNodeId { get; }
        public Guid TerminalEquipmentId { get; }
        public ushort StructureIndex { get; }
        public ushort TerminalIndex { get; }
        public Guid TerminalId => Id;
        public bool IsDummyEnd => TerminalId == Guid.Empty;
        public bool IsSimpleTerminal => TerminalEquipmentId == Guid.Empty;

        public UtilityGraphConnectedTerminal(Guid id, Guid terminalEquipmentId, Guid nodeOfInterestId, ushort structureIndex = 0, ushort terminalIndex = 0) : base(id)
        {
            TerminalEquipmentId = terminalEquipmentId;
            RouteNodeId = nodeOfInterestId;
            StructureIndex = structureIndex;
            TerminalIndex = terminalIndex; 
        }

        public override string ToString()
        {
            if (IsSimpleTerminal)
                return $"Simple Terminal: {Id} at equipment: {TerminalEquipmentId} route node: {RouteNodeId}";
            else if (IsDummyEnd)
                return $"Dummy Terminal: {Id} at route node: {RouteNodeId}";
            else
                return $"Terminal: {Id} at equipment: {TerminalEquipmentId} at route node: {RouteNodeId}";
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
