using DAX.ObjectVersioning.Core;
using DAX.ObjectVersioning.Graph;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.Business.Graph.Projections
{
    /// <summary>
    /// Maintains a graph of span and terminal equipments by analyzing changes between equipment states
    /// Created to avoid implementing and maintaining projection functions for each aggregate event type that might come along
    /// </summary>
    public static class UtilityGraphSegmentProjections
    {
        public static void ApplyConnectivityChangesToGraph(SpanEquipment spanEquipmentBefore, SpanEquipment spanEquipmentAfter, UtilityGraph graph)
        {
            var trans = graph.CreateTransaction();

            ProcessRemovedStructures(spanEquipmentBefore, spanEquipmentAfter, graph, trans);
            ProcessAddedStructures(spanEquipmentBefore, spanEquipmentAfter, graph, trans);
            ProcessRemovedSpanSegments(spanEquipmentBefore, spanEquipmentAfter, graph, trans);
            ProcessAddedSpanSegments(spanEquipmentBefore, spanEquipmentAfter, graph, trans);
            ProcessUpdatedSpanSegments(spanEquipmentBefore, spanEquipmentAfter, graph, trans);

            trans.Commit();
        }

        private static void ProcessRemovedStructures(SpanEquipment spanEquipmentBefore, SpanEquipment spanEquipmentAfter, UtilityGraph graph, ITransaction trans)
        {
            // Iterate all structures in before span equipment state, and check if structures have been removed
            for (int beforeStructureIndex = 0; beforeStructureIndex < spanEquipmentBefore.SpanStructures.Length; beforeStructureIndex++)
            {
                var beforeStructure = spanEquipmentBefore.SpanStructures[beforeStructureIndex];

                // Check if structure has been truncated away or marked as deleted
                if (beforeStructure.Deleted == false && ((spanEquipmentAfter.SpanStructures.Length <= beforeStructureIndex) || spanEquipmentAfter.SpanStructures[beforeStructureIndex].Deleted == true))
                {
                    // Remove all segments within the structure
                    foreach (var segment in beforeStructure.SpanSegments)
                        RemoveSegmentIfExists(segment.Id, graph, trans);
                }
            }
        }

        private static void ProcessAddedStructures(SpanEquipment spanEquipmentBefore, SpanEquipment spanEquipmentAfter, UtilityGraph graph, ITransaction trans)
        {
            // Iterate all structures in after span equipment state, and check if structures have been added
            for (int afterStructureIndex = 0; afterStructureIndex < spanEquipmentAfter.SpanStructures.Length; afterStructureIndex++)
            {
                var afterStructure = spanEquipmentAfter.SpanStructures[afterStructureIndex];

                // Check if structure has been added or unmarked as deleted
                if (spanEquipmentBefore.SpanStructures.Length <= afterStructureIndex || (spanEquipmentBefore.SpanStructures[afterStructureIndex].Deleted == true && spanEquipmentAfter.SpanStructures[afterStructureIndex].Deleted == false))
                {
                    // Add all segments within the structure
                    for (int segmentIndex = 0; segmentIndex < afterStructure.SpanSegments.Length; segmentIndex++)
                        AddSegment(spanEquipmentAfter, (ushort)afterStructureIndex, (ushort)segmentIndex, graph, trans);
                }
            }
        }

        private static void ProcessRemovedSpanSegments(SpanEquipment spanEquipmentBefore, SpanEquipment spanEquipmentAfter, UtilityGraph graph, ITransaction trans)
        {
            // Iterate all structures in before span equipment state, and check if segments have been removed in the after equipment state
            for (int structureIndex = 0; structureIndex < spanEquipmentBefore.SpanStructures.Length; structureIndex++)
            {
                var beforeStructure = spanEquipmentBefore.SpanStructures[structureIndex];

                // We only need to process structures that also exists in after span equipment, because segments removed as part of structure removal are handled in ProcessRemovedStructures
                if (spanEquipmentAfter.SpanStructures.Length > structureIndex)
                {
                    var afterStructure = spanEquipmentAfter.SpanStructures[structureIndex];

                    // Remove all segments that don't exists in after structure
                    foreach (var fromSegment in beforeStructure.SpanSegments)
                    {
                        // Try find after segment the old fashined way, because Linq is slower that this code
                        bool afterSegmentFound = false;

                        foreach (var afterSegment in afterStructure.SpanSegments)
                        {
                            if (afterSegment.Id == fromSegment.Id)
                                afterSegmentFound = true;
                        }

                        if (!afterSegmentFound)
                            RemoveSegmentIfExists(fromSegment.Id, graph, trans);
                    }
                }
            }
        }

        private static void ProcessAddedSpanSegments(SpanEquipment spanEquipmentBefore, SpanEquipment spanEquipmentAfter, UtilityGraph graph, ITransaction trans)
        {
            // Iterate all structures in before span equipment state, and check if segments have been added in the after equipment state
            for (int structureIndex = 0; structureIndex < spanEquipmentBefore.SpanStructures.Length; structureIndex++)
            {
                var beforeStructure = spanEquipmentBefore.SpanStructures[structureIndex];

                // We only need to process structures that also exists in after span equipment, because new segments added in new structures are handled in ProcessAddedStructures
                if (spanEquipmentAfter.SpanStructures.Length > structureIndex)
                {
                    var afterStructure = spanEquipmentAfter.SpanStructures[structureIndex];

                    // Add all after segments that don't exists in before structure
                    for (int afterSegmentIndex = 0; afterSegmentIndex < afterStructure.SpanSegments.Length; afterSegmentIndex++)
                    {
                        var afterSegment = afterStructure.SpanSegments[afterSegmentIndex];

                        // Try find before segment the old fashined way, because Linq is slower that this code
                        bool beforeSegmentFound = false;

                        foreach (var beforeSegment in beforeStructure.SpanSegments)
                        {
                            if (beforeSegment.Id == afterSegment.Id)
                                beforeSegmentFound = true;
                        }

                        if (!beforeSegmentFound)
                            AddSegment(spanEquipmentAfter, (ushort)structureIndex, (ushort)afterSegmentIndex, graph, trans);
                    }
                }
            }
        }

        private static void ProcessUpdatedSpanSegments(SpanEquipment spanEquipmentBefore, SpanEquipment spanEquipmentAfter, UtilityGraph graph, ITransaction trans)
        {
            // Iterate all structures in before span equipment state, and check if segments terminal ids or indexes have been updated
            for (int structureIndex = 0; structureIndex < spanEquipmentBefore.SpanStructures.Length; structureIndex++)
            {
                var beforeStructure = spanEquipmentBefore.SpanStructures[structureIndex];

                // We only need to process structures that also exists in after span equipment
                if (spanEquipmentAfter.SpanStructures.Length > structureIndex)
                {
                    var afterStructure = spanEquipmentAfter.SpanStructures[structureIndex];

                    // Iterate through after segments and check of before segments has changed
                    for (int afterSegmentIndex = 0; afterSegmentIndex < afterStructure.SpanSegments.Length; afterSegmentIndex++)
                    {
                        var afterSegment = afterStructure.SpanSegments[afterSegmentIndex];

                        // Try find before segment changed the old fashined way, because Linq is slower that this code
                        bool afterSegmentHasChanged = false;

                        for (int beforeSegmentIndex = 0; beforeSegmentIndex < beforeStructure.SpanSegments.Length; beforeSegmentIndex++)
                        {
                            var beforeSegment = beforeStructure.SpanSegments[beforeSegmentIndex];

                            if (beforeSegment.Id == afterSegment.Id)
                            {
                                if (beforeSegment.FromTerminalId != afterSegment.FromTerminalId || beforeSegment.ToTerminalId != afterSegment.ToTerminalId || beforeSegmentIndex != afterSegmentIndex)
                                    afterSegmentHasChanged = true;
                            }
                        }

                        if (afterSegmentHasChanged)
                            UpdateSegment(spanEquipmentAfter, (ushort)structureIndex, (ushort)afterSegmentIndex, graph, trans);
                    }
                }
            }
        }

        private static void AddSegment(SpanEquipment spanEquipment, UInt16 structureIndex, UInt16 segmentIndex, UtilityGraph graph, ITransaction transaction)
        {
            var spanSegment = spanEquipment.SpanStructures[structureIndex].SpanSegments[segmentIndex];

            // If a disconnected segment
            if (spanSegment.FromTerminalId == Guid.Empty && spanSegment.ToTerminalId == Guid.Empty)
            {
                graph.AddDisconnectedSegment(spanEquipment, structureIndex, segmentIndex);
            }
            else
            {
                IUtilityGraphTerminalRef? fromTerminal = null;

                if (spanSegment.FromTerminalId != Guid.Empty)
                    fromTerminal = FindOrCreateSimpleTerminal(spanSegment.FromTerminalId, spanEquipment.NodesOfInterestIds[spanSegment.FromNodeOfInterestIndex], graph, transaction);

                IUtilityGraphTerminalRef? toTerminal = null;

                if (spanSegment.ToTerminalId != Guid.Empty)
                    toTerminal = FindOrCreateSimpleTerminal(spanSegment.ToTerminalId, spanEquipment.NodesOfInterestIds[spanSegment.ToNodeOfInterestIndex], graph, transaction);

                var newSegmentGraphElement = new UtilityGraphConnectedSegment(spanSegment.Id, fromTerminal as GraphNode, toTerminal as GraphNode, spanEquipment.Id, structureIndex, segmentIndex);

                transaction.Add(newSegmentGraphElement);

                graph.AddToIndex(spanSegment.Id, newSegmentGraphElement);
            }
        }

        private static void UpdateSegment(SpanEquipment spanEquipment, UInt16 structureIndex, UInt16 segmentIndex, UtilityGraph graph, ITransaction transaction)
        {
            var spanSegment = spanEquipment.SpanStructures[structureIndex].SpanSegments[segmentIndex];

            // If the segment has become a disconnected segment
            if (spanSegment.FromTerminalId == Guid.Empty && spanSegment.ToTerminalId == Guid.Empty)
            {
                // Get rid of connected graph element if such exists
                if (graph.TryGetGraphElement<UtilityGraphConnectedSegment>(spanSegment.Id, out var _))
                {
                    transaction.Delete(spanSegment.Id);
                }

                graph.UpdateIndex(spanSegment.Id, new UtilityGraphDisconnectedSegment(spanEquipment.Id, structureIndex, segmentIndex));
            }
            else
            {
                IUtilityGraphTerminalRef? fromTerminal = null;

                if (spanSegment.FromTerminalId != Guid.Empty)
                    fromTerminal = FindOrCreateSimpleTerminal(spanSegment.FromTerminalId, spanEquipment.NodesOfInterestIds[spanSegment.FromNodeOfInterestIndex], graph, transaction);

                IUtilityGraphTerminalRef? toTerminal = null;

                if (spanSegment.ToTerminalId != Guid.Empty)
                    toTerminal = FindOrCreateSimpleTerminal(spanSegment.ToTerminalId, spanEquipment.NodesOfInterestIds[spanSegment.ToNodeOfInterestIndex], graph, transaction);

                var updatedSegmentGraphElement = new UtilityGraphConnectedSegment(spanSegment.Id, fromTerminal as GraphNode, toTerminal as GraphNode, spanEquipment.Id, structureIndex, segmentIndex);

                if (graph.TryGetGraphElement<UtilityGraphConnectedSegment>(spanSegment.Id, out var _))
                {
                    transaction.Update(updatedSegmentGraphElement);
                }
                else
                {
                    transaction.Add(updatedSegmentGraphElement);
                }

                graph.UpdateIndex(spanSegment.Id, updatedSegmentGraphElement);
            }
        }

        private static void RemoveSegmentIfExists(Guid segmentId, UtilityGraph graph, ITransaction transaction)
        {
            // Get rid of segment if exists
            if (graph.TryGetGraphElement<IUtilityGraphSegmentRef>(segmentId, out var utilityGraphSegmentRef))
            {
                // Remove it from the dict containing all segments in the system
                graph.RemoveFromIndex(segmentId);

                // If the existing segment is a non-disconnected segment, then remove it from the versioned graph as well
                if (!(utilityGraphSegmentRef is UtilityGraphDisconnectedSegment))
                    transaction.Delete(segmentId);
            }
        }
                
        private static IUtilityGraphTerminalRef FindOrCreateSimpleTerminal(Guid terminalId, Guid terminalNodeOfInterestId, UtilityGraph graph, ITransaction transaction)
        {
            // Try find terminal in graph
            var terminal = graph.GetTerminal(terminalId, transaction.Version.InternalVersionId);

            // Try find in transaction
            if (terminal == null)
            {
                terminal = transaction.GetObject(terminalId) as IUtilityGraphTerminalRef;
            }

            if (terminal == null)
            {
                if (graph.TryGetGraphElement<IUtilityGraphTerminalRef>(terminalId, out var utilityGraphTerminalRef))
                {
                    terminal = new UtilityGraphConnectedTerminal(terminalId, utilityGraphTerminalRef.TerminalEquipmentId, terminalNodeOfInterestId, utilityGraphTerminalRef.StructureIndex, utilityGraphTerminalRef.TerminalIndex);
                    transaction.Add(terminal as GraphNode);
                    graph.UpdateIndex(terminalId, terminal);
                }
                else
                {
                    terminal = new UtilityGraphConnectedTerminal(terminalId, Guid.Empty, terminalNodeOfInterestId);
                    transaction.Add(terminal as GraphNode);
                }
            }

            return terminal;
        }
        
    }
}
