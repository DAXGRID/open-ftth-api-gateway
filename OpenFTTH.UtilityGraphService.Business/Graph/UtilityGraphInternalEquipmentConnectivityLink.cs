using DAX.ObjectVersioning.Graph;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.Business.Graph
{
    public class UtilityGraphInternalEquipmentConnectivityLink : GraphEdge, IUtilityInternalEquipmentConnectivityRef
    {
        public Guid RouteNodeId { get; }
        public Guid TerminalEquipmentId { get; }
        public ushort StructureIndex { get; }

        public UtilityGraphInternalEquipmentConnectivityLink(Guid id, GraphNode? fromNode, GraphNode? toNode, Guid terminalEquipmentId, Guid nodeOfInterestId, ushort structureIndex = 0) : base(id, fromNode, toNode)
        {
            TerminalEquipmentId = terminalEquipmentId;
            RouteNodeId = nodeOfInterestId;
            StructureIndex = structureIndex;
        }

        public override string ToString()
        {
            return $"Connectivity link in terminal equipment: {TerminalEquipmentId} structure index: {StructureIndex} route node: {RouteNodeId}";
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
    }
}

