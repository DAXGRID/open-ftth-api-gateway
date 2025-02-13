using DAX.ObjectVersioning.Core;
using DAX.ObjectVersioning.Graph;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.NodeContainers.Events;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.UtilityGraphService.Business.Graph.Projections
{
    /// <summary>
    /// Write up internal connectivity in terminal equipments - i.e. in splitters and wdm modules
    /// </summary>
    public static class UtilityGraphTerminalEquipmentProjections
    {
        private static UtilityGraphConnectedTerminal CreateOrGetTerminal(Guid terminalId, Guid terminalNodeOfInterestId, UtilityGraph graph, ITransaction transaction)
        {
            // Try find terminal in graph
            var terminalRef = graph.GetTerminal(terminalId, transaction.Version.InternalVersionId);

            // Try find in transaction
            if (terminalRef == null)
            {
                terminalRef = transaction.GetObject(terminalId) as IUtilityGraphTerminalRef;
            }

            if (terminalRef == null)
            {
                if (graph.TryGetGraphElement<IUtilityGraphTerminalRef>(terminalId, out var utilityGraphTerminalRef))
                {
                    var terminal = new UtilityGraphConnectedTerminal(terminalId, utilityGraphTerminalRef.TerminalEquipmentId, terminalNodeOfInterestId, utilityGraphTerminalRef.StructureIndex, utilityGraphTerminalRef.TerminalIndex);
                    transaction.Add(terminal);
                    graph.UpdateIndex(terminalId, terminal);

                    return terminal;
                }
                else
                {
                    var terminal = new UtilityGraphConnectedTerminal(terminalId, Guid.Empty, terminalNodeOfInterestId);
                    transaction.Add(terminal);

                    return terminal;
                }
            }
            else
            {
                return (UtilityGraphConnectedTerminal)terminalRef;
            }
        }

        public static void ApplyNewTerminalToTerminalConnectionToGraph(NodeContainerTerminalsConnected @event, Guid routeNodeId, UtilityGraph graph)
        {
            var trans = graph.CreateTransaction();

            var fromTerminal = CreateOrGetTerminal(@event.FromTerminalId, routeNodeId, graph, trans);

            var toTerminal = CreateOrGetTerminal(@event.ToTerminalId, routeNodeId, graph, trans);

            var newTerminalToTerminalConnection = new UtilityGraphTerminalToTerminalConnectivityLink(@event.ConnectionId, fromTerminal, toTerminal, routeNodeId, @event.FiberCoordLength);

            trans.Add(newTerminalToTerminalConnection);

            trans.Commit();
        }

        public static void ApplyConnectivityToGraph(TerminalEquipment terminalEquipment, NodeContainer nodeContainer, UtilityGraph graph)
        {
            AddTerminalStructuresConnectivityToGraph(terminalEquipment, nodeContainer, graph);
        }

        public static void ApplyConnectivityToGraph(TerminalEquipment terminalEquipment, NodeContainer nodeContainer, HashSet<Guid> structuresToAdd, UtilityGraph graph)
        {
            AddTerminalStructuresConnectivityToGraph(terminalEquipment, nodeContainer, graph, structuresToAdd);
        }

        private static void AddTerminalStructuresConnectivityToGraph(TerminalEquipment terminalEquipment, NodeContainer nodeContainer, UtilityGraph graph, HashSet<Guid>? structuresToAdd = null)
        {
            HashSet<Guid> internalNodes = new();

            // Add terminals to the graph
            for (UInt16 structureIndex = 0; structureIndex < terminalEquipment.TerminalStructures.Length; structureIndex++)
            {
                var terminalStructure = terminalEquipment.TerminalStructures[structureIndex];

                if (structuresToAdd != null && !structuresToAdd.Contains(terminalStructure.Id))
                    continue;

                for (UInt16 terminalIndex = 0; terminalIndex < terminalStructure.Terminals.Length; terminalIndex++)
                {
                    var terminal = terminalStructure.Terminals[terminalIndex];

                    // We're dealing with a virgin terminal
                    graph.AddDisconnectedTerminal(nodeContainer.RouteNodeId, terminalEquipment, terminal.Id, structureIndex, terminalIndex);

                    // Add eventually internal node
                    if (terminal.InternalConnectivityNodeId != null && terminal.InternalConnectivityNodeId != Guid.Empty)
                        internalNodes.Add(terminal.InternalConnectivityNodeId.Value);
                }
            }

            // If we're dealing with a terminal equipment with internal nodes, we need connect them in the graph
            if (internalNodes.Count > 0)
            {
                // First create all the internal nodes in the graph
                ApplyInternalConnectivityToGraph(nodeContainer, terminalEquipment, graph, structuresToAdd);
            }
        }
               

        public static void ApplyInternalConnectivityToGraph(NodeContainer nodeContainer, TerminalEquipment terminalEquipment, UtilityGraph graph, HashSet<Guid>? structuresToConnect = null)
        {
            var trans = graph.CreateTransaction();

            Dictionary<Guid, UtilityGraphInternalEquipmentConnectivityNode> connectivityNodeCreated = new();

            for (int structureIndex = 0; structureIndex < terminalEquipment.TerminalStructures.Length; structureIndex++)
            {
                var terminalStructure = terminalEquipment.TerminalStructures[structureIndex];
                
                if (structuresToConnect != null && !structuresToConnect.Contains(terminalStructure.Id))
                    continue;

                foreach (var terminal in terminalStructure.Terminals)
                {
                    if (terminal.InternalConnectivityNodeId != null && terminal.InternalConnectivityNodeId != Guid.Empty)
                    {
                        Guid internalConnectivityNodeId = terminal.InternalConnectivityNodeId.Value;

                        // Create connectivity node if not already created
                        if (!connectivityNodeCreated.ContainsKey(internalConnectivityNodeId))
                        {
                            var newInternalConnectivityNode = new UtilityGraphInternalEquipmentConnectivityNode(internalConnectivityNodeId, terminalEquipment.Id, nodeContainer.RouteNodeId, (ushort)structureIndex);

                            connectivityNodeCreated[internalConnectivityNodeId] = newInternalConnectivityNode;

                            trans.Add(newInternalConnectivityNode);
                        }

                        // Add terminal to graph
                        var connectedTerminal = CreateOrGetTerminal(terminal.Id, nodeContainer.RouteNodeId, graph, trans);

                        // Connect terminal with internal connectivity node
                        if (terminal.Direction == TerminalDirectionEnum.IN)
                        {
                            var internalConnectivityLink = new UtilityGraphInternalEquipmentConnectivityLink(Guid.NewGuid(), connectedTerminal, connectivityNodeCreated[internalConnectivityNodeId], terminalEquipment.Id, nodeContainer.RouteNodeId, (ushort)structureIndex);
                            trans.Add(internalConnectivityLink);
                        }
                        else
                        {
                            var internalConnectivityLink = new UtilityGraphInternalEquipmentConnectivityLink(Guid.NewGuid(), connectivityNodeCreated[internalConnectivityNodeId], connectedTerminal, terminalEquipment.Id, nodeContainer.RouteNodeId, (ushort)structureIndex);
                            trans.Add(internalConnectivityLink);
                        }
                    }
                }
            }

            trans.Commit();
        }

        public static void ApplyTerminalToTerminalDisconnectedToGraph(NodeContainerTerminalsDisconnected @event, Guid routeNodeId, UtilityGraph graph)
        {
            var trans = graph.CreateTransaction();

            var version = trans.Version.InternalVersionId;

            // Find from terminal
            if (!graph.TryGetGraphElement<UtilityGraphConnectedTerminal>(@event.FromTerminalId, out var fromTerminal))
                return;

            // Find segment connecting the two terminals
            var terminalToTerminalConnection = fromTerminal.NeighborElements(version).OfType<UtilityGraphTerminalToTerminalConnectivityLink>().First(e => e.InV(version).Id == @event.ToTerminalId || e.OutV(version).Id == @event.ToTerminalId);

            if (terminalToTerminalConnection != null)
            {
                trans.Delete(terminalToTerminalConnection.Id);
            }

            trans.Commit();
        }
    }
}
