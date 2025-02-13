using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.NodeContainers.Events;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Events;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace OpenFTTH.UtilityGraphService.Business.Graph.Projections
{
    /// <summary>
    /// Functions that apply events to a span equipment immutable object and return a new copy
    /// </summary>
    public static class SpanEquipmentProjectionFunctions
    {
        public static SpanEquipment Apply(SpanEquipment existingSpanEquipment, SpanEquipmentNamingInfoChanged @event)
        {
            return existingSpanEquipment with
            {
                NamingInfo = @event.NamingInfo
            };
        }

        public static SpanEquipment Apply(SpanEquipment existingSpanEquipment, SpanSegmentsCut spanSegmentsCutEvent)
        {
            // Cut them segments
            List<SpanStructure> newStructures = new List<SpanStructure>();

            // Create dictionary of cuts for fast lookup
            Dictionary<Guid, SpanSegmentCutInfo> spanSegmentCutInfoBySegmentId = spanSegmentsCutEvent.Cuts.ToDictionary(c => c.OldSpanSegmentId);

            // First create a new nodes of interest list with the cut node added
            Guid[] newNodeOfInterestIdList = CreateNewNodeOfInterestIdListWithExtraNode(existingSpanEquipment, spanSegmentsCutEvent.CutNodeOfInterestId, spanSegmentsCutEvent.CutNodeOfInterestIndex);

            bool nodeOfInterestAlreadyExists = existingSpanEquipment.NodesOfInterestIds.Contains(spanSegmentsCutEvent.CutNodeOfInterestId);

            // Loop though all span structures
            for (UInt16 structureIndex = 0; structureIndex < existingSpanEquipment.SpanStructures.Length; structureIndex++)
            {
                var existingSpanStructure = existingSpanEquipment.SpanStructures[structureIndex];

                List<SpanSegment> newSegments = new List<SpanSegment>();

                // Loop throughh all span segments
                foreach (var existingSegment in existingSpanStructure.SpanSegments)
                {
                    UInt16 fromNodeOfInterestIndexToUse = existingSegment.FromNodeOfInterestIndex;
                    UInt16 toNodeOfInterestIndexToUse = existingSegment.ToNodeOfInterestIndex;

                    if (!nodeOfInterestAlreadyExists)
                    {
                        if (fromNodeOfInterestIndexToUse >= spanSegmentsCutEvent.CutNodeOfInterestIndex)
                            fromNodeOfInterestIndexToUse++;

                        if (toNodeOfInterestIndexToUse >= spanSegmentsCutEvent.CutNodeOfInterestIndex)
                            toNodeOfInterestIndexToUse++;
                    }

                    // If cut info exists
                    if (spanSegmentCutInfoBySegmentId.TryGetValue(existingSegment.Id, out var spanSegmentCutInfo))
                    {
                        // Add the first segment
                        newSegments.Add(
                            new SpanSegment(
                                id: spanSegmentCutInfo.NewSpanSegmentId1,
                                fromNodeOfInterestIndex: fromNodeOfInterestIndexToUse,
                                toNodeOfInterestIndex: spanSegmentsCutEvent.CutNodeOfInterestIndex
                            )
                            {
                                FromTerminalId = existingSegment.FromTerminalId
                            }
                        );

                        // Add the second segment
                        newSegments.Add(
                            new SpanSegment(
                                id: spanSegmentCutInfo.NewSpanSegmentId2,
                                fromNodeOfInterestIndex: spanSegmentsCutEvent.CutNodeOfInterestIndex,
                                toNodeOfInterestIndex: toNodeOfInterestIndexToUse
                            )
                            {
                                ToTerminalId = existingSegment.ToTerminalId
                            }
                        );
                    }
                    // If no cut info exists
                    else
                    {
                        if (!nodeOfInterestAlreadyExists)
                        {
                            var newSegment = existingSegment with { FromNodeOfInterestIndex = fromNodeOfInterestIndexToUse, ToNodeOfInterestIndex = toNodeOfInterestIndexToUse };
                            newSegments.Add(newSegment);
                        }
                        else
                        {
                            newSegments.Add(existingSegment);
                        }
                    }
                }

                newStructures.Add(
                    existingSpanStructure with
                    {
                        SpanSegments = newSegments.ToArray()
                    });
            }

            return existingSpanEquipment with
            {
                NodesOfInterestIds = newNodeOfInterestIdList,
                SpanStructures = newStructures.ToArray()
            };
        }

        public static SpanEquipment Apply(SpanEquipment existingSpanEquipment, SpanEquipmentCutReverted spanEquipmentCutReverted)
        {
            UInt16 uncutNodeOfInterestIndex = (UInt16) Array.IndexOf(existingSpanEquipment.NodesOfInterestIds, spanEquipmentCutReverted.CutNodeOfInterestId);

            var newNodeOfInterestIdList = CreateNewNodeOfInterestIdListWithRemovedNode(existingSpanEquipment, spanEquipmentCutReverted.CutNodeOfInterestId);

            List<SpanStructure> newStructures = new List<SpanStructure>();

            Dictionary<Guid, SpanSegmentCutRevertInfo> revertInfoLookup = spanEquipmentCutReverted.Reverts.ToDictionary(r => r.OldSpanSegmentId2);

            // Loop though all span structures
            for (UInt16 structureIndex = 0; structureIndex < existingSpanEquipment.SpanStructures.Length; structureIndex++)
            {
                var existingSpanStructure = existingSpanEquipment.SpanStructures[structureIndex];

                List<SpanSegment> newSegments = new List<SpanSegment>();

                // Loop through all span segments
                SpanSegment? previousSegment = null;

                foreach (var existingSegment in existingSpanStructure.SpanSegments)
                {
                    UInt16 fromNodeOfInterestIndexToUse = existingSegment.FromNodeOfInterestIndex;
                    UInt16 toNodeOfInterestIndexToUse = existingSegment.ToNodeOfInterestIndex;

                    if (fromNodeOfInterestIndexToUse > uncutNodeOfInterestIndex)
                        fromNodeOfInterestIndexToUse--;

                    if (toNodeOfInterestIndexToUse > uncutNodeOfInterestIndex)
                        toNodeOfInterestIndexToUse--;

                    if (existingSegment.ToNodeOfInterestIndex == uncutNodeOfInterestIndex)
                    {
                        // We're dealing with the left part segment of the cut to be reverted - we just skip/delete this one
                    }
                    else if (existingSegment.FromNodeOfInterestIndex == uncutNodeOfInterestIndex)
                    {
                        // We're dealing with the right part segment of the cut to be reverted - now it's time to do stuff

                        if (previousSegment == null)
                            throw new ApplicationException($"Invalid span structure! Found segment: {existingSegment.Id} connected *from* node: {spanEquipmentCutReverted.CutNodeOfInterestId}, but no segments found connected *to* that node in the span structure: {existingSpanStructure.Id}");

                        var revertInfo = revertInfoLookup[existingSegment.Id];

                        var newSegment = new SpanSegment(revertInfo.NewSpanSegmentId, previousSegment.FromNodeOfInterestIndex, toNodeOfInterestIndexToUse)
                        {
                            FromTerminalId = previousSegment.FromTerminalId,
                            ToTerminalId = existingSegment.ToTerminalId
                        };

                        newSegments.Add(newSegment);
                    }
                    else  
                    {
                        // We're dealing with an segment not referencing the node to be reverted
                        var newSegment = existingSegment with { FromNodeOfInterestIndex = fromNodeOfInterestIndexToUse, ToNodeOfInterestIndex = toNodeOfInterestIndexToUse };
                        newSegments.Add(newSegment);
                    }

                    previousSegment = existingSegment;
                }

                newStructures.Add(
                    existingSpanStructure with
                    {
                        SpanSegments = newSegments.ToArray()
                    });
            }

            return existingSpanEquipment with
            {
                NodesOfInterestIds = newNodeOfInterestIdList,
                SpanStructures = newStructures.ToArray()
            };
        }

        public static SpanEquipment Apply(SpanEquipment existingSpanEquipment, SpanEquipmentAffixedToContainer spanEquipmentAffixedToContainer)
        {
            var newListOfAffixes = new List<SpanEquipmentNodeContainerAffix>();

            if (existingSpanEquipment.NodeContainerAffixes != null)
            {
                foreach (var existingAffix in existingSpanEquipment.NodeContainerAffixes)
                    newListOfAffixes.Add(existingAffix);
            }

            newListOfAffixes.Add(spanEquipmentAffixedToContainer.Affix);

            return existingSpanEquipment with
            {
                NodeContainerAffixes = newListOfAffixes.ToArray()
            };
        }

        public static SpanEquipment Apply(SpanEquipment existingSpanEquipment, SpanEquipmentAffixSideChanged spanEquipmentAffixSideChanged)
        {
            var newListOfAffixes = new List<SpanEquipmentNodeContainerAffix>();

            if (existingSpanEquipment.NodeContainerAffixes != null)
            {
                foreach (var existingAffix in existingSpanEquipment.NodeContainerAffixes)
                {
                    if (existingAffix.NodeContainerId == spanEquipmentAffixSideChanged.NodeContainerId)
                    {
                        var newAffix = new SpanEquipmentNodeContainerAffix(existingAffix.RouteNodeId, existingAffix.NodeContainerId, spanEquipmentAffixSideChanged.NodeContainerIngoingSide);
                        newListOfAffixes.Add(newAffix);
                    }
                    else
                    {
                        newListOfAffixes.Add(existingAffix);
                    }
                }
            }

            return existingSpanEquipment with
            {
                NodeContainerAffixes = newListOfAffixes.ToArray()
            };
        }

        public static SpanEquipment Apply(SpanEquipment existingSpanEquipment, SpanEquipmentDetachedFromContainer @event)
        {
            var newListOfAffixes = new List<SpanEquipmentNodeContainerAffix>();

            if (existingSpanEquipment.NodeContainerAffixes != null)
            {
                foreach (var existingAffix in existingSpanEquipment.NodeContainerAffixes)
                {
                    if (existingAffix.NodeContainerId != @event.NodeContainerId)
                        newListOfAffixes.Add(existingAffix);
                }
            }
            
            return existingSpanEquipment with
            {
                NodeContainerAffixes = newListOfAffixes.ToArray()
            };
        }

        private static Guid[] CreateNewNodeOfInterestIdListWithExtraNode(SpanEquipment existingSpanEquipment, Guid cutNodeOfInterestId, UInt16 newNodeOfInterestIndex)
        {
            if (existingSpanEquipment.NodesOfInterestIds.Contains(cutNodeOfInterestId))
                return existingSpanEquipment.NodesOfInterestIds;

            var result = new List<Guid>();

            for (UInt16 i = 0; i < newNodeOfInterestIndex; i++)
            {
                result.Add(existingSpanEquipment.NodesOfInterestIds[i]);
            }

            result.Add(cutNodeOfInterestId);

            for (UInt16 i = newNodeOfInterestIndex; i < existingSpanEquipment.NodesOfInterestIds.Length; i++)
            {
                result.Add(existingSpanEquipment.NodesOfInterestIds[i]);
            }

            return result.ToArray();
        }

        private static Guid[] CreateNewNodeOfInterestIdListWithRemovedNode(SpanEquipment existingSpanEquipment, Guid uncutNodeOfInterestId)
        {
            var result = new List<Guid>();

            for (UInt16 i = 0; i < existingSpanEquipment.NodesOfInterestIds.Length; i++)
            {
                if (existingSpanEquipment.NodesOfInterestIds[i] != uncutNodeOfInterestId)
                    result.Add(existingSpanEquipment.NodesOfInterestIds[i]);
            }

            return result.ToArray();
        }

        public static SpanEquipment Apply(SpanEquipment existingSpanEquipment, SpanSegmentsConnectedToSimpleTerminals @event)
        {
            List<SpanStructure> newStructures = new List<SpanStructure>();

            // Create dictionary of cuts for fast lookup
            Dictionary<Guid, SpanSegmentToSimpleTerminalConnectInfo> spanSegmentConnectInfoBySegmentId = @event.Connects.ToDictionary(c => c.SegmentId);

            // Loop though all span structures
            for (UInt16 structureIndex = 0; structureIndex < existingSpanEquipment.SpanStructures.Length; structureIndex++)
            {
                var existingSpanStructure = existingSpanEquipment.SpanStructures[structureIndex];

                List<SpanSegment> newSegments = new List<SpanSegment>();

                // Loop through all span segments
                foreach (var existingSegment in existingSpanStructure.SpanSegments)
                {
                    // If connect info exists
                    if (spanSegmentConnectInfoBySegmentId.TryGetValue(existingSegment.Id, out var spanSegmentConnectInfo))
                    {
                        if (spanSegmentConnectInfo.ConnectionDirection == SpanSegmentToTerminalConnectionDirection.FromSpanSegmentToTerminal)
                        {
                            newSegments.Add(
                                existingSegment with { ToTerminalId = spanSegmentConnectInfo.TerminalId }
                            );
                        }
                        else
                        {
                            newSegments.Add(
                                existingSegment with { FromTerminalId = spanSegmentConnectInfo.TerminalId }
                            );
                        }
                    }
                    else
                    {
                        newSegments.Add(existingSegment);
                    }
                }

                newStructures.Add(
                    existingSpanStructure with
                    {
                        SpanSegments = newSegments.ToArray()
                    }
                );
            }

            return existingSpanEquipment with
            {
                SpanStructures = newStructures.ToArray()
            };
        }

        public static SpanEquipment Apply(SpanEquipment existingSpanEquipment, SpanSegmentDisconnectedFromTerminal @event)
        {
            List<SpanStructure> newStructures = new List<SpanStructure>();

            // Loop though all span structures
            for (UInt16 structureIndex = 0; structureIndex < existingSpanEquipment.SpanStructures.Length; structureIndex++)
            {
                var existingSpanStructure = existingSpanEquipment.SpanStructures[structureIndex];

                List<SpanSegment> newSegments = new List<SpanSegment>();

                // Loop through all span segments
                foreach (var existingSegment in existingSpanStructure.SpanSegments)
                {
                    // If disconnect
                    if (existingSegment.Id == @event.SpanSegmentId)
                    {
                        if (existingSegment.FromTerminalId == @event.TerminalId)
                        {
                            newSegments.Add(
                                existingSegment with { FromTerminalId = Guid.Empty }
                            );
                        }
                        else if(existingSegment.ToTerminalId == @event.TerminalId)
                        {
                            newSegments.Add(
                                existingSegment with { ToTerminalId = Guid.Empty }
                            );
                        }
                    }
                    else
                    {
                        newSegments.Add(existingSegment);
                    }
                }

                newStructures.Add(
                    existingSpanStructure with
                    {
                        SpanSegments = newSegments.ToArray()
                    }
                );
            }

            return existingSpanEquipment with
            {
                SpanStructures = newStructures.ToArray()
            };
        }

        public static SpanEquipment Apply(SpanEquipment existingSpanEquipment, SpanSegmentsDisconnectedFromTerminals @event)
        {
            List<SpanStructure> newStructures = new List<SpanStructure>();

            Dictionary<Guid, SpanSegmentToTerminalDisconnectInfo> disconnectInfoBySpanEquipmentId  = @event.Disconnects.ToDictionary(e => e.SegmentId);

            // Loop though all span structures
            for (UInt16 structureIndex = 0; structureIndex < existingSpanEquipment.SpanStructures.Length; structureIndex++)
            {
                var existingSpanStructure = existingSpanEquipment.SpanStructures[structureIndex];

                List<SpanSegment> newSegments = new List<SpanSegment>();

                // Loop through all span segments
                foreach (var existingSegment in existingSpanStructure.SpanSegments)
                {
                    // If disconnect
                    if (disconnectInfoBySpanEquipmentId.ContainsKey(existingSegment.Id))
                    {
                        var terminalId = disconnectInfoBySpanEquipmentId[existingSegment.Id].TerminalId;

                        if (existingSegment.FromTerminalId == terminalId)
                        {
                            newSegments.Add(
                                existingSegment with { FromTerminalId = Guid.Empty }
                            );
                        }
                        else if (existingSegment.ToTerminalId == terminalId)
                        {
                            newSegments.Add(
                                existingSegment with { ToTerminalId = Guid.Empty }
                            );
                        }
                    }
                    else
                    {
                        newSegments.Add(existingSegment);
                    }
                }

                newStructures.Add(
                    existingSpanStructure with
                    {
                        SpanSegments = newSegments.ToArray()
                    }
                );
            }

            return existingSpanEquipment with
            {
                SpanStructures = newStructures.ToArray()
            };
        }

        public static SpanEquipment Apply(SpanEquipment existingSpanEquipment, AdditionalStructuresAddedToSpanEquipment @event)
        {
            List<SpanStructure> newStructures = new List<SpanStructure>();

            // Copy all span structures
            foreach (var existingStructure in existingSpanEquipment.SpanStructures)
            {
                newStructures.Add(existingStructure);
            }

            // Add new ones
            newStructures.AddRange(@event.SpanStructuresToAdd);

            return existingSpanEquipment with
            {
                SpanStructures = newStructures.ToArray()
            };
        }

        public static SpanEquipment Apply(SpanEquipment existingSpanEquipment, SpanStructureRemoved @event)
        {
            List<SpanStructure> newStructures = new();

            // Copy all span structures
            foreach (var existingStructure in existingSpanEquipment.SpanStructures)
            {
                if (existingStructure.Id == @event.SpanStructureId)
                {
                    newStructures.Add(existingStructure with { Deleted = true });
                }
                else
                {
                    newStructures.Add(existingStructure);
                }
            }

            return existingSpanEquipment with
            {
                SpanStructures = newStructures.ToArray()
            };
        }

        public static SpanEquipment Apply(SpanEquipment existingSpanEquipment, SpanEquipmentMoved @event)
        {
            return existingSpanEquipment with
            {
                NodesOfInterestIds = @event.NodesOfInterestIds
            };
        }

        public static SpanEquipment Apply(SpanEquipment existingSpanEquipment, SpanEquipmentMerged @event)
        {
            return existingSpanEquipment with
            {
                NodesOfInterestIds = @event.NodesOfInterestIds
            };
        }

        public static SpanEquipment Apply(SpanEquipment existingSpanEquipment, SpanEquipmentMarkingInfoChanged @event)
        {
            return existingSpanEquipment with
            {
                MarkingInfo = @event.MarkingInfo
            };
        }

        public static SpanEquipment Apply(SpanEquipment existingSpanEquipment, SpanEquipmentAddressInfoChanged @event)
        {
            return existingSpanEquipment with
            {
                AddressInfo = @event.AddressInfo
            };
        }

        public static SpanEquipment Apply(SpanEquipment existingSpanEquipment, SpanEquipmentManufacturerChanged @event)
        {
            return existingSpanEquipment with
            {
                ManufacturerId = @event.ManufacturerId
            };
        }

        public static SpanEquipment Apply(SpanEquipment existingSpanEquipment, SpanEquipmentSpecificationChanged @event)
        {
            List<SpanStructure> newSpanStructures = new List<SpanStructure>();

            Dictionary<Guid, StructureModificationInstruction> modificationInstructionByStructureId = @event.StructureModificationInstructions.ToDictionary(s => s.StructureId);

            // Modify and delete the existing structures
            foreach (var existingSpanStructure in existingSpanEquipment.SpanStructures)
            {
                if (modificationInstructionByStructureId.ContainsKey(existingSpanStructure.Id))
                {
                    var instruction = modificationInstructionByStructureId[existingSpanStructure.Id];
                    
                    if (instruction.StructureToBeDeleted == true)
                    {
                        // Don't copy the existing structure
                    }
                    else if (instruction.StructureSpecificationIdToBeUpdated != null)
                    {
                        newSpanStructures.Add(existingSpanStructure with { SpecificationId = instruction.StructureSpecificationIdToBeUpdated.Value });
                    }
                }
            }

            // Add eventually new structures
            foreach (var instruction in @event.StructureModificationInstructions)
            {
                if (instruction.NewStructureToBeInserted != null)
                {
                    newSpanStructures.Add(instruction.NewStructureToBeInserted);
                }

            }

            return existingSpanEquipment with
            {
                SpecificationId = @event.NewSpecificationId,
                SpanStructures = newSpanStructures.ToArray()
            };
        }

        public static SpanEquipment Apply(SpanEquipment existingSpanEquipment, SpanEquipmentAffixedToParent @event)
        {
            return existingSpanEquipment with
            {
                UtilityNetworkHops = @event.NewUtilityHopList
            };
        }

        public static SpanEquipment Apply(SpanEquipment existingSpanEquipment, SpanEquipmentDetachedFromParent @event)
        {
            return existingSpanEquipment with
            {
                UtilityNetworkHops = @event.NewUtilityHopList
            };
        }
    }
}
