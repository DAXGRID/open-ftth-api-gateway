using OpenFTTH.EventSourcing;
using OpenFTTH.UtilityGraphService.API.Model;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph.Projections;
using OpenFTTH.UtilityGraphService.Business.NodeContainers.Events;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.Graph
{
    public class UtilityNetworkProjection : ProjectionBase
    {
        private readonly ConcurrentDictionary<Guid, SpanEquipment> _spanEquipmentByEquipmentId = new();
        private readonly ConcurrentDictionary<Guid, TerminalEquipment> _terminalEquipmentByEquipmentId = new();
        private readonly ConcurrentDictionary<string, HashSet<Guid>> _terminalEquipmentIdByName = new();
        private readonly ConcurrentDictionary<Guid, SpanEquipment> _spanEquipmentByInterestId = new();
        private readonly ConcurrentDictionary<Guid, NodeContainer> _nodeContainerByEquipmentId = new();
        private readonly ConcurrentDictionary<Guid, NodeContainer> _nodeContainerByInterestId = new();
        private readonly ConcurrentDictionary<Guid, List<Guid>> _relatedCablesByConduitSegmentId = new();

        private readonly UtilityGraph _utilityGraph;

        public UtilityGraph Graph => _utilityGraph;

        public IReadOnlyDictionary<Guid, NodeContainer> NodeContainerByEquipmentId => _nodeContainerByEquipmentId;

        public IReadOnlyDictionary<Guid, NodeContainer> NodeContainerByInterestId => _nodeContainerByInterestId;

        public IReadOnlyDictionary<Guid, SpanEquipment> SpanEquipmentsByEquipmentId => _spanEquipmentByEquipmentId;

        public IReadOnlyDictionary<Guid, SpanEquipment> SpanEquipmentsByInterestId => _spanEquipmentByInterestId;

        public IReadOnlyDictionary<Guid, TerminalEquipment> TerminalEquipmentByEquipmentId => _terminalEquipmentByEquipmentId;

        public IReadOnlyDictionary<string, HashSet<Guid>> TerminalEquipmentIdByName => _terminalEquipmentIdByName;

        public IReadOnlyDictionary<Guid, List<Guid>> RelatedCablesByConduitSegmentId => _relatedCablesByConduitSegmentId;


        public UtilityNetworkProjection()
        {
            _utilityGraph = new(this);

            // Span equipment
            ProjectEventAsync<SpanEquipmentPlacedInRouteNetwork>(ProjectAsync);
            ProjectEventAsync<SpanEquipmentNamingInfoChanged>(ProjectAsync);
            ProjectEventAsync<SpanEquipmentAffixedToContainer>(ProjectAsync);
            ProjectEventAsync<SpanEquipmentAffixSideChanged>(ProjectAsync);
            ProjectEventAsync<SpanEquipmentDetachedFromContainer>(ProjectAsync);
            ProjectEventAsync<SpanSegmentsCut>(ProjectAsync);
            ProjectEventAsync<SpanEquipmentCutReverted>(ProjectAsync);
            ProjectEventAsync<SpanSegmentsConnectedToSimpleTerminals>(ProjectAsync);
            ProjectEventAsync<SpanSegmentDisconnectedFromTerminal>(ProjectAsync);
            ProjectEventAsync<SpanSegmentsDisconnectedFromTerminals>(ProjectAsync);
            ProjectEventAsync<AdditionalStructuresAddedToSpanEquipment>(ProjectAsync);
            ProjectEventAsync<SpanStructureRemoved>(ProjectAsync);
            ProjectEventAsync<SpanEquipmentRemoved>(ProjectAsync);
            ProjectEventAsync<SpanEquipmentMoved>(ProjectAsync);
            ProjectEventAsync<SpanEquipmentMerged>(ProjectAsync);
            ProjectEventAsync<SpanEquipmentMarkingInfoChanged>(ProjectAsync);
            ProjectEventAsync<SpanEquipmentAddressInfoChanged>(ProjectAsync);
            ProjectEventAsync<SpanEquipmentManufacturerChanged>(ProjectAsync);
            ProjectEventAsync<SpanEquipmentSpecificationChanged>(ProjectAsync);
            ProjectEventAsync<SpanEquipmentAffixedToParent>(ProjectAsync);
            ProjectEventAsync<SpanEquipmentDetachedFromParent>(ProjectAsync);

            // Terminal equipment
            ProjectEventAsync<TerminalEquipmentPlacedInNodeContainer>(ProjectAsync);
            ProjectEventAsync<TerminalEquipmentNamingInfoChanged>(ProjectAsync);
            ProjectEventAsync<TerminalEquipmentAddressInfoChanged>(ProjectAsync);
            ProjectEventAsync<TerminalEquipmentManufacturerChanged>(ProjectAsync);
            ProjectEventAsync<TerminalEquipmentSpecificationChanged>(ProjectAsync);
            ProjectEventAsync<TerminalEquipmentRemoved>(ProjectAsync);
            ProjectEventAsync<AdditionalStructuresAddedToTerminalEquipment>(ProjectAsync);
            ProjectEventAsync<TerminalStructureRemoved>(ProjectAsync);
            ProjectEventAsync<TerminalStructureInterfaceInfoChanged>(ProjectAsync);

            // Node container
            ProjectEventAsync<NodeContainerPlacedInRouteNetwork>(ProjectAsync);
            ProjectEventAsync<NodeContainerRemovedFromRouteNetwork>(ProjectAsync);
            ProjectEventAsync<NodeContainerManufacturerChanged>(ProjectAsync);
            ProjectEventAsync<NodeContainerSpecificationChanged>(ProjectAsync);
            ProjectEventAsync<NodeContainerVerticalAlignmentReversed>(ProjectAsync);
            ProjectEventAsync<NodeContainerRackAdded>(ProjectAsync);
            ProjectEventAsync<NodeContainerRackRemoved>(ProjectAsync);
            ProjectEventAsync<NodeContainerRackSpecificationChanged>(ProjectAsync);
            ProjectEventAsync<NodeContainerRackNameChanged>(ProjectAsync);
            ProjectEventAsync<NodeContainerRackHeightInUnitsChanged>(ProjectAsync);
            ProjectEventAsync<NodeContainerTerminalEquipmentAdded>(ProjectAsync);
            ProjectEventAsync<NodeContainerTerminalEquipmentsAddedToRack>(ProjectAsync);
            ProjectEventAsync<NodeContainerTerminalEquipmentReferenceRemoved>(ProjectAsync);
            ProjectEventAsync<NodeContainerTerminalEquipmentMovedToRack>(ProjectAsync);
            ProjectEventAsync<NodeContainerTerminalsConnected>(ProjectAsync);
            ProjectEventAsync<NodeContainerTerminalsDisconnected>(ProjectAsync);
        }

        public bool TryGetEquipment<T>(Guid equipmentOrInterestId, out T equipment) where T: IEquipment
        {
            if (_spanEquipmentByEquipmentId.TryGetValue(equipmentOrInterestId, out SpanEquipment? spanEquipmentByEquipmentId))
            {
                if (spanEquipmentByEquipmentId is T)
                {
                    equipment = (T)(object)spanEquipmentByEquipmentId;
                    return true;
                }
            }
            else if (_spanEquipmentByInterestId.TryGetValue(equipmentOrInterestId, out SpanEquipment? spanEquipmentByInterestId))
            {
                if (spanEquipmentByInterestId is T)
                {
                    equipment = (T)(object)spanEquipmentByInterestId;
                    return true;
                }
            }
            else if (_terminalEquipmentByEquipmentId.TryGetValue(equipmentOrInterestId, out TerminalEquipment? terminalEquipmentByEquipmentId))
            {
                if (terminalEquipmentByEquipmentId is T)
                {
                    equipment = (T)(object)terminalEquipmentByEquipmentId;
                    return true;
                }
            }
            else if (_nodeContainerByEquipmentId.TryGetValue(equipmentOrInterestId, out NodeContainer? nodeContainerByEquipmentId))
            {
                if (nodeContainerByEquipmentId is T)
                {
                    equipment = (T)(object)nodeContainerByEquipmentId;
                    return true;
                }
            }
            else if (_nodeContainerByInterestId.TryGetValue(equipmentOrInterestId, out NodeContainer? nodeContainerByInterestId))
            {
                if (nodeContainerByInterestId is T)
                {
                    equipment = (T)(object)nodeContainerByInterestId;
                    return true;
                }
            }
            else if (_utilityGraph.TryGetGraphElement<IUtilityGraphSegmentRef>(equipmentOrInterestId, out var utilityGraphSegmentRef))
            {
                if (utilityGraphSegmentRef.SpanEquipment(this) is T)
                {
                    equipment = (T)(object)utilityGraphSegmentRef.SpanEquipment(this);
                    return true;
                }
            }

            #pragma warning disable CS8601 // Possible null reference assignment.
            equipment = default(T);
            #pragma warning restore CS8601 // Possible null reference assignment.

            return false;
        }

        public bool CheckIfConduitSegmentContainsCables(Guid conduitSpanSegmentId)
        {
            if (_relatedCablesByConduitSegmentId.ContainsKey(conduitSpanSegmentId) && _relatedCablesByConduitSegmentId[conduitSpanSegmentId].Count > 0)
                return true;
            else
                return false;
        }
        

        private Task ProjectAsync(IEventEnvelope eventEnvelope)
        {
            switch (eventEnvelope.Data)
            {
                // Span equipment events
                case (SpanEquipmentPlacedInRouteNetwork @event):
                    StoreAndIndexVirginSpanEquipment(@event.Equipment);
                    break;


                case (SpanEquipmentNamingInfoChanged @event):
                    var oldSpanEquipmentState = _spanEquipmentByEquipmentId[@event.SpanEquipmentId];

                    var newSpanEquipmentState = SpanEquipmentProjectionFunctions.Apply(oldSpanEquipmentState, @event);

                    TryUpdate(newSpanEquipmentState);

                    break;

                case (AdditionalStructuresAddedToSpanEquipment @event):
                    ProcessAdditionalStructuresAddToSpanEquipment(@event);
                    break;

                case (SpanStructureRemoved @event):
                    ProcessInnerStructureRemoval(@event);
                    break;

                case (SpanEquipmentAffixedToContainer @event):
                    TryUpdate(SpanEquipmentProjectionFunctions.Apply(_spanEquipmentByEquipmentId[@event.SpanEquipmentId], @event));
                    break;

                case (SpanEquipmentAffixSideChanged @event):
                    TryUpdate(SpanEquipmentProjectionFunctions.Apply(_spanEquipmentByEquipmentId[@event.SpanEquipmentId], @event));
                    break;

                case (SpanEquipmentDetachedFromContainer @event):
                    TryUpdate(SpanEquipmentProjectionFunctions.Apply(_spanEquipmentByEquipmentId[@event.SpanEquipmentId], @event));
                    break;

                case (SpanSegmentsCut @event):
                    ProcesstSegmentCuts(@event);
                    break;

                case (SpanEquipmentCutReverted @event):
                    ProcesstSpanEquipmentCutReverted(@event);
                    break;

                case (SpanSegmentsConnectedToSimpleTerminals @event):
                    ProcessSegmentConnects(@event);
                    break;

                case (SpanSegmentDisconnectedFromTerminal @event):
                    ProcessSegmentDisconnects(@event);
                    break;

                case (SpanSegmentsDisconnectedFromTerminals @event):
                    ProcessSegmentDisconnects(@event);
                    break;

                case (SpanEquipmentRemoved @event):
                    ProcessSpanEquipmentRemoval(@event);
                    break;

                case (SpanEquipmentMoved @event):
                    TryUpdate(SpanEquipmentProjectionFunctions.Apply(_spanEquipmentByEquipmentId[@event.SpanEquipmentId], @event));
                    break;

                case (SpanEquipmentMerged @event):
                    ProcessSpanEquipmentMerge(@event);
                    break;

                case (SpanEquipmentMarkingInfoChanged @event):
                    TryUpdate(SpanEquipmentProjectionFunctions.Apply(_spanEquipmentByEquipmentId[@event.SpanEquipmentId], @event));
                    break;

                case (SpanEquipmentAddressInfoChanged @event):
                    TryUpdate(SpanEquipmentProjectionFunctions.Apply(_spanEquipmentByEquipmentId[@event.SpanEquipmentId], @event));
                    break;

                case (SpanEquipmentManufacturerChanged @event):
                    TryUpdate(SpanEquipmentProjectionFunctions.Apply(_spanEquipmentByEquipmentId[@event.SpanEquipmentId], @event));
                    break;

                case (SpanEquipmentSpecificationChanged @event):
                    ProcessSpanEquipmentSpecificationChange(@event);
                    break;

                case (SpanEquipmentAffixedToParent @event):
                   ProcessSpanEquipmentParentAffix(@event);
                   break;

                case (SpanEquipmentDetachedFromParent @event):
                    ProcessSpanEquipmentParentDetach(@event);
                    break;


                // Terminal equipment events
                case (TerminalEquipmentPlacedInNodeContainer @event):
                    StoreAndIndexVirginTerminalEquipment(@event.Equipment);
                    break;

                case (TerminalEquipmentNamingInfoChanged @event):
                    var oldEquipmentState = _terminalEquipmentByEquipmentId[@event.TerminalEquipmentId];

                    RemoveTerminalEquipmentFromNameIndex(oldEquipmentState);

                    var newEquipmentState = TerminalEquipmentProjectionFunctions.Apply(oldEquipmentState, @event);

                    TryUpdate(newEquipmentState);

                    AddTerminalEquipmentToNameIndex(newEquipmentState);
                    break;

                case (TerminalEquipmentAddressInfoChanged @event):
                    TryUpdate(TerminalEquipmentProjectionFunctions.Apply(_terminalEquipmentByEquipmentId[@event.TerminalEquipmentId], @event));
                    break;

                case (TerminalEquipmentManufacturerChanged @event):
                    TryUpdate(TerminalEquipmentProjectionFunctions.Apply(_terminalEquipmentByEquipmentId[@event.TerminalEquipmentId], @event));
                    break;

                case (TerminalEquipmentSpecificationChanged @event):
                    TryUpdate(TerminalEquipmentProjectionFunctions.Apply(_terminalEquipmentByEquipmentId[@event.TerminalEquipmentId], @event));
                    break;

                case (TerminalEquipmentRemoved @event):
                    ProcessTerminalEquipmentRemoval(@event);
                    break;

                case (AdditionalStructuresAddedToTerminalEquipment @event):
                    ProcessAdditionalStructuresAddedToTerminalEquipment(@event);
                    break;

                case (TerminalStructureRemoved @event):
                    ProcessTerminalEquipmentStructureRemoval(@event);
                    break;

                case (TerminalStructureInterfaceInfoChanged @event):
                    TryUpdate(TerminalEquipmentProjectionFunctions.Apply(_terminalEquipmentByEquipmentId[@event.TerminalEquipmentId], @event));
                    break;


                // Node container events
                case (NodeContainerPlacedInRouteNetwork @event):
                    StoreAndIndexVirginContainerEquipment(@event.Container);
                    break;

                case (NodeContainerRemovedFromRouteNetwork @event):
                    ProcessNodeContainerRemoval(@event);
                    break;

                case (NodeContainerVerticalAlignmentReversed @event):
                    TryUpdate(NodeContainerProjectionFunctions.Apply(_nodeContainerByEquipmentId[@event.NodeContainerId], @event));
                    break;

                case (NodeContainerManufacturerChanged @event):
                    TryUpdate(NodeContainerProjectionFunctions.Apply(_nodeContainerByEquipmentId[@event.NodeContainerId], @event));
                    break;

                case (NodeContainerSpecificationChanged @event):
                    TryUpdate(NodeContainerProjectionFunctions.Apply(_nodeContainerByEquipmentId[@event.NodeContainerId], @event));
                    break;

                case (NodeContainerRackAdded @event):
                    TryUpdate(NodeContainerProjectionFunctions.Apply(_nodeContainerByEquipmentId[@event.NodeContainerId], @event));
                    break;

                case (NodeContainerRackRemoved @event):
                    TryUpdate(NodeContainerProjectionFunctions.Apply(_nodeContainerByEquipmentId[@event.NodeContainerId], @event));
                    break;

                case (NodeContainerRackSpecificationChanged @event):
                    TryUpdate(NodeContainerProjectionFunctions.Apply(_nodeContainerByEquipmentId[@event.NodeContainerId], @event));
                    break;

                case (NodeContainerRackNameChanged @event):
                    TryUpdate(NodeContainerProjectionFunctions.Apply(_nodeContainerByEquipmentId[@event.NodeContainerId], @event));
                    break;

                case (NodeContainerRackHeightInUnitsChanged @event):
                    TryUpdate(NodeContainerProjectionFunctions.Apply(_nodeContainerByEquipmentId[@event.NodeContainerId], @event));
                    break;

                case (NodeContainerTerminalEquipmentAdded @event):
                    TryUpdate(NodeContainerProjectionFunctions.Apply(_nodeContainerByEquipmentId[@event.NodeContainerId], @event));
                    break;

                case (NodeContainerTerminalEquipmentsAddedToRack @event):
                    TryUpdate(NodeContainerProjectionFunctions.Apply(_nodeContainerByEquipmentId[@event.NodeContainerId], @event));
                    break;

                case (NodeContainerTerminalEquipmentReferenceRemoved @event):
                    TryUpdate(NodeContainerProjectionFunctions.Apply(_nodeContainerByEquipmentId[@event.NodeContainerId], @event));
                    break;

                case (NodeContainerTerminalEquipmentMovedToRack @event):
                    TryUpdate(NodeContainerProjectionFunctions.Apply(_nodeContainerByEquipmentId[@event.NodeContainerId], @event));
                    break;

                case (NodeContainerTerminalsConnected @event):
                    ProcessTerminalsConnected(@event);
                    break;

                case (NodeContainerTerminalsDisconnected @event):
                    ProcessTerminalsDisconnected(@event);
                    break;
            }

            return Task.CompletedTask;
        }


        private void ProcessTerminalsConnected(NodeContainerTerminalsConnected @event)
        {
            var existingNodeContainer = _nodeContainerByEquipmentId[@event.NodeContainerId];
            TryUpdate(NodeContainerProjectionFunctions.Apply(existingNodeContainer, @event));

            UtilityGraphTerminalEquipmentProjections.ApplyNewTerminalToTerminalConnectionToGraph(@event, existingNodeContainer.RouteNodeId, _utilityGraph);
        }

        private void ProcessTerminalsDisconnected(NodeContainerTerminalsDisconnected @event)
        {
            var existingNodeContainer = _nodeContainerByEquipmentId[@event.NodeContainerId];
            TryUpdate(NodeContainerProjectionFunctions.Apply(existingNodeContainer, @event));

            UtilityGraphTerminalEquipmentProjections.ApplyTerminalToTerminalDisconnectedToGraph(@event, existingNodeContainer.RouteNodeId, _utilityGraph);
        }


        private void ProcessSpanEquipmentParentDetach(SpanEquipmentDetachedFromParent @event)
        {
            var existingSpanEquipment = _spanEquipmentByEquipmentId[@event.SpanEquipmentId];
            var newSpanEquipment = SpanEquipmentProjectionFunctions.Apply(existingSpanEquipment, @event);
            TryUpdate(newSpanEquipment);

            UpdateRelatedCableIndex(@event.NewUtilityHopList, existingSpanEquipment);
        }

        private void ProcessSpanEquipmentParentAffix(SpanEquipmentAffixedToParent @event)
        {
            var existingSpanEquipment = _spanEquipmentByEquipmentId[@event.SpanEquipmentId];
            var newSpanEquipment = SpanEquipmentProjectionFunctions.Apply(existingSpanEquipment, @event);
            TryUpdate(newSpanEquipment);

            UpdateRelatedCableIndex(@event.NewUtilityHopList, existingSpanEquipment);
        }

        private void UpdateRelatedCableIndex(UtilityNetworkHop[] newUtilityNetworkHopList, SpanEquipment existingSpanEquipment)
        {
            // Update segment to cable index
            HashSet<Guid> existingSegmentWhereToRemoveCableRel = new();

            if (existingSpanEquipment.UtilityNetworkHops != null && existingSpanEquipment.UtilityNetworkHops.Length > 0)
            {
                foreach (var utilityHop in existingSpanEquipment.UtilityNetworkHops)
                {
                    foreach (var affix in utilityHop.ParentAffixes)
                    {
                        existingSegmentWhereToRemoveCableRel.Add(affix.SpanSegmentId);
                    }
                }
            }

            HashSet<Guid> segmentIdsWhereToAddCableRel = new();

            foreach (var utilityHop in newUtilityNetworkHopList)
            {
                foreach (var affix in utilityHop.ParentAffixes)
                {
                    if (existingSegmentWhereToRemoveCableRel.Contains(affix.SpanSegmentId))
                    {
                        existingSegmentWhereToRemoveCableRel.Remove(affix.SpanSegmentId);
                    }
                    else
                    {
                        segmentIdsWhereToAddCableRel.Add(affix.SpanSegmentId);
                    }
                }
            }

            // Remove cable ids from index
            foreach (var segmentIdWhereToRemoveCabelRel in existingSegmentWhereToRemoveCableRel)
            {
                if (_relatedCablesByConduitSegmentId.ContainsKey(segmentIdWhereToRemoveCabelRel))
                {
                    var existingValue = _relatedCablesByConduitSegmentId[segmentIdWhereToRemoveCabelRel];

                    List<Guid> newValue = new();
                    foreach (var cableId in existingValue)
                    {
                        if (cableId != existingSpanEquipment.Id)
                            newValue.Add(cableId);
                    }

                    if (!_relatedCablesByConduitSegmentId.TryUpdate(segmentIdWhereToRemoveCabelRel, newValue, existingValue))
                        throw new ApplicationException($"Concurrent exception trying to update conduit segment to cable index. Cable with id: {existingSpanEquipment.Id}");
                }
            }


            // Add cable ids from index
            foreach (var segmentIdWhereToAddCabelRel in segmentIdsWhereToAddCableRel)
            {
                _relatedCablesByConduitSegmentId.AddOrUpdate(
                             segmentIdWhereToAddCabelRel,
                             new List<Guid> { existingSpanEquipment.Id },
                             (key, oldValue) =>
                             {
                                 var newList = new List<Guid> { existingSpanEquipment.Id };
                                 newList.AddRange(oldValue);
                                 return newList;
                             }
                          );
            }
        }

        private void StoreAndIndexVirginSpanEquipment(SpanEquipment spanEquipment)
        {
            // Store the new span equipment in memory
            _spanEquipmentByEquipmentId.TryAdd(spanEquipment.Id, spanEquipment);
            _spanEquipmentByInterestId.TryAdd(spanEquipment.WalkOfInterestId, spanEquipment);

            // Add span segments to the graph
            for (UInt16 structureIndex = 0; structureIndex < spanEquipment.SpanStructures.Length; structureIndex++)
            {
                // We're dealing with a virgin span equipment and therefore only disconnected segments at index 0
                _utilityGraph.AddDisconnectedSegment(spanEquipment, structureIndex, 0);
            }

            // Index conduit relations
            if (spanEquipment.UtilityNetworkHops != null && spanEquipment.UtilityNetworkHops.Length > 0)
            {
                foreach (var utilityHop in spanEquipment.UtilityNetworkHops)
                {
                    foreach (var parentAffix in utilityHop.ParentAffixes)
                    {
                        _relatedCablesByConduitSegmentId.AddOrUpdate(
                            parentAffix.SpanSegmentId, 
                            new List<Guid> { spanEquipment.Id },
                            (key, oldValue) => {
                                var newList = new List<Guid> { spanEquipment.Id };
                                newList.AddRange(oldValue);
                                return newList; 
                            }
                         );
                    }
                }
            }
        }

        private void StoreAndIndexVirginTerminalEquipment(TerminalEquipment terminalEquipment)
        {
            // Store the new terminal equipment in memory by id
            _terminalEquipmentByEquipmentId.TryAdd(terminalEquipment.Id, terminalEquipment);

            AddTerminalEquipmentToNameIndex(terminalEquipment);

            var nodeContainer = _nodeContainerByEquipmentId[terminalEquipment.NodeContainerId];

            UtilityGraphTerminalEquipmentProjections.ApplyConnectivityToGraph(terminalEquipment, nodeContainer, Graph);
        }

        private void ProcessAdditionalStructuresAddedToTerminalEquipment(AdditionalStructuresAddedToTerminalEquipment @event)
        {
            var before = _terminalEquipmentByEquipmentId[@event.TerminalEquipmentId];
            var after = TerminalEquipmentProjectionFunctions.Apply(before, @event);
            TryUpdate(after);

            var nodeContainer = _nodeContainerByEquipmentId[after.NodeContainerId];

            UtilityGraphTerminalEquipmentProjections.ApplyConnectivityToGraph(after, nodeContainer, @event.TerminalStructuresToAdd.Select(t => t.Id).ToHashSet(), Graph);
        }

        private void ProcessTerminalEquipmentStructureRemoval(TerminalStructureRemoved @event)
        {
            var existingTerminalEquipment = _terminalEquipmentByEquipmentId[@event.TerminalEquipmentId];
            var after = TerminalEquipmentProjectionFunctions.Apply(existingTerminalEquipment, @event);
            TryUpdate(after);

            // Remove terminals from the graph
            foreach (var terminalStructure in existingTerminalEquipment.TerminalStructures)
            {
                if (terminalStructure.Id == @event.TerminalStructureId)
                {
                    foreach (var terminal in terminalStructure.Terminals)
                    {
                        _utilityGraph.RemoveGraphElement(terminal.Id);
                    }
                }
            }
        }

        private void StoreAndIndexVirginContainerEquipment(NodeContainer nodeContainer)
        {
            // Store the new span equipment in memory
            _nodeContainerByEquipmentId.TryAdd(nodeContainer.Id, nodeContainer);
            _nodeContainerByInterestId.TryAdd(nodeContainer.InterestId, nodeContainer);
        }

        private void ProcesstSegmentCuts(SpanSegmentsCut @event)
        {
            var before = _spanEquipmentByEquipmentId[@event.SpanEquipmentId];
            var after = SpanEquipmentProjectionFunctions.Apply(_spanEquipmentByEquipmentId[@event.SpanEquipmentId], @event);
            TryUpdate(after);

            UtilityGraphSegmentProjections.ApplyConnectivityChangesToGraph(before, after, _utilityGraph);
        }

        private void ProcesstSpanEquipmentCutReverted(SpanEquipmentCutReverted @event)
        {
            var before = _spanEquipmentByEquipmentId[@event.SpanEquipmentId];
            var after = SpanEquipmentProjectionFunctions.Apply(_spanEquipmentByEquipmentId[@event.SpanEquipmentId], @event);
            TryUpdate(after);

            UtilityGraphSegmentProjections.ApplyConnectivityChangesToGraph(before, after, _utilityGraph);
        }

        private void ProcessSegmentConnects(SpanSegmentsConnectedToSimpleTerminals @event)
        {
            var before = _spanEquipmentByEquipmentId[@event.SpanEquipmentId];
            var after = SpanEquipmentProjectionFunctions.Apply(_spanEquipmentByEquipmentId[@event.SpanEquipmentId], @event);
            TryUpdate(after);

            UtilityGraphSegmentProjections.ApplyConnectivityChangesToGraph(before, after, _utilityGraph);
        }

        private void ProcessSegmentDisconnects(SpanSegmentDisconnectedFromTerminal @event)
        {
            var before = _spanEquipmentByEquipmentId[@event.SpanEquipmentId];
            var after = SpanEquipmentProjectionFunctions.Apply(_spanEquipmentByEquipmentId[@event.SpanEquipmentId], @event);
            TryUpdate(after);

            UtilityGraphSegmentProjections.ApplyConnectivityChangesToGraph(before, after, _utilityGraph);
        }

        private void ProcessSegmentDisconnects(SpanSegmentsDisconnectedFromTerminals @event)
        {
            var before = _spanEquipmentByEquipmentId[@event.SpanEquipmentId];
            var after = SpanEquipmentProjectionFunctions.Apply(_spanEquipmentByEquipmentId[@event.SpanEquipmentId], @event);
            TryUpdate(after);

            UtilityGraphSegmentProjections.ApplyConnectivityChangesToGraph(before, after, _utilityGraph);
        }

        private void ProcessAdditionalStructuresAddToSpanEquipment(AdditionalStructuresAddedToSpanEquipment @event)
        {
            var before = _spanEquipmentByEquipmentId[@event.SpanEquipmentId];
            var after = SpanEquipmentProjectionFunctions.Apply(_spanEquipmentByEquipmentId[@event.SpanEquipmentId], @event);
            TryUpdate(after);

            UtilityGraphSegmentProjections.ApplyConnectivityChangesToGraph(before, after, _utilityGraph);
        }

        private void ProcessInnerStructureRemoval(SpanStructureRemoved @event)
        {
            var before = _spanEquipmentByEquipmentId[@event.SpanEquipmentId];
            var after = SpanEquipmentProjectionFunctions.Apply(_spanEquipmentByEquipmentId[@event.SpanEquipmentId], @event);
            TryUpdate(after);

            UtilityGraphSegmentProjections.ApplyConnectivityChangesToGraph(before, after, _utilityGraph);
        }

        private void ProcessSpanEquipmentRemoval(SpanEquipmentRemoved @event)
        {
            var existingSpanEquipment = _spanEquipmentByEquipmentId[@event.SpanEquipmentId];

            // Remove references from conduit index
            if (existingSpanEquipment.UtilityNetworkHops != null)
            {
                foreach (var hop in existingSpanEquipment.UtilityNetworkHops)
                {
                    foreach (var affix in hop.ParentAffixes)
                    {
                        if (_relatedCablesByConduitSegmentId.TryGetValue(affix.SpanSegmentId, out var cableIds))
                        {
                            cableIds.Remove(existingSpanEquipment.Id);
                        }
                    }
                }
            }


            TryRemoveSpanEquipment(@event.SpanEquipmentId, existingSpanEquipment.WalkOfInterestId);

            // Remove span segments from the graph
            foreach (var spanStructure in existingSpanEquipment.SpanStructures)
            {
                if (!spanStructure.Deleted)
                {
                    foreach (var spanSegment in spanStructure.SpanSegments)
                    {
                        _utilityGraph.RemoveGraphElement(spanSegment.Id);
                    }
                }
            }
        }

        private void ProcessTerminalEquipmentRemoval(TerminalEquipmentRemoved @event)
        {
            var existingTerminalEquipment = _terminalEquipmentByEquipmentId[@event.TerminalEquipmentId];

            TryRemoveTerminalEquipment(@event.TerminalEquipmentId);

            RemoveTerminalEquipmentFromNameIndex(existingTerminalEquipment);

            // Remove terminals from the graph
            foreach (var terminalStructure in existingTerminalEquipment.TerminalStructures)
            {
                if (!terminalStructure.Deleted)
                {
                    foreach (var terminal in terminalStructure.Terminals)
                    {
                        _utilityGraph.RemoveGraphElement(terminal.Id);
                    }
                }
            }
        }

        private void ProcessNodeContainerRemoval(NodeContainerRemovedFromRouteNetwork @event)
        {
            var existingNodeContainer = _nodeContainerByEquipmentId[@event.NodeContainerId];

            TryRemoveNodeContainer(@event.NodeContainerId, existingNodeContainer.InterestId);
        }

        private void ProcessSpanEquipmentSpecificationChange(SpanEquipmentSpecificationChanged @event)
        {
            var before = _spanEquipmentByEquipmentId[@event.SpanEquipmentId];
            var after = SpanEquipmentProjectionFunctions.Apply(_spanEquipmentByEquipmentId[@event.SpanEquipmentId], @event);
            TryUpdate(after);

            UtilityGraphSegmentProjections.ApplyConnectivityChangesToGraph(before, after, _utilityGraph);
        }

        private void ProcessSpanEquipmentMerge(SpanEquipmentMerged @event)
        {
            TryUpdate(SpanEquipmentProjectionFunctions.Apply(_spanEquipmentByEquipmentId[@event.SpanEquipmentId], @event));
        }

        private void TryUpdate(SpanEquipment newSpanEquipmentState)
        {
            var oldSpanEquipment = _spanEquipmentByEquipmentId[newSpanEquipmentState.Id];

            if (!_spanEquipmentByEquipmentId.TryUpdate(newSpanEquipmentState.Id, newSpanEquipmentState, oldSpanEquipment))
                throw new ApplicationException($"Concurrency issue updating span equipment index. Span equipment id: {newSpanEquipmentState.Id} Please make sure that events are applied in sequence to the projection.");

            if (!_spanEquipmentByInterestId.TryUpdate(newSpanEquipmentState.WalkOfInterestId, newSpanEquipmentState, oldSpanEquipment))
                throw new ApplicationException($"Concurrency issue updating span equipment interest index. Span equipment id: {newSpanEquipmentState.Id} Please make sure that events are applied in sequence to the projection.");
        }

        private void TryUpdate(TerminalEquipment newTerminalEquipmentState)
        {
            var oldTerminalEquipment = _terminalEquipmentByEquipmentId[newTerminalEquipmentState.Id];

            if (!_terminalEquipmentByEquipmentId.TryUpdate(newTerminalEquipmentState.Id, newTerminalEquipmentState, oldTerminalEquipment))
                throw new ApplicationException($"Concurrency issue updating terminal equipment index. Terminal equipment id: {newTerminalEquipmentState.Id} Please make sure that events are applied in sequence to the projection.");
        }

        private void TryUpdate(NodeContainer newNodeContainerState)
        {
            var oldEquipment = _nodeContainerByEquipmentId[newNodeContainerState.Id];

            if (!_nodeContainerByEquipmentId.TryUpdate(newNodeContainerState.Id, newNodeContainerState, oldEquipment))
                throw new ApplicationException($"Concurrency issue updating node container equipment index. Node container equipment id: {newNodeContainerState.Id} Please make sure that events are applied in sequence to the projection.");

            if (!_nodeContainerByInterestId.TryUpdate(newNodeContainerState.InterestId, newNodeContainerState, oldEquipment))
                throw new ApplicationException($"Concurrency issue updating node container equipment interest index. Node container equipment id: {newNodeContainerState.Id} Please make sure that events are applied in sequence to the projection.");
        }

        private void TryRemoveSpanEquipment(Guid spanEquipmentId, Guid spanEquipmentInterestId)
        {
            if (!_spanEquipmentByEquipmentId.TryRemove(spanEquipmentId, out _))
                throw new ApplicationException($"Concurrency issue removing span equipment index. Span equipment id: {spanEquipmentId} Please make sure that events are applied in sequence to the projection.");

            if (!_spanEquipmentByInterestId.TryRemove(spanEquipmentInterestId, out _))
                throw new ApplicationException($"Concurrency issue removing span equipment interest index. Span equipment id: {spanEquipmentId} Please make sure that events are applied in sequence to the projection.");
        }

        private void TryRemoveTerminalEquipment(Guid terminalEquipmentId)
        {
            if (!_terminalEquipmentByEquipmentId.TryRemove(terminalEquipmentId, out _))
                throw new ApplicationException($"Concurrency issue removing teminal equipment from index. Terminal equipment id: {terminalEquipmentId} Please make sure that events are applied in sequence to the projection.");
        }

        private void TryRemoveNodeContainer(Guid nodeContainertId, Guid nodeContainerInterestId)
        {
            if (!_nodeContainerByEquipmentId.TryRemove(nodeContainertId, out _))
                throw new ApplicationException($"Concurrency issue removing node container from equipment dictionary. Node container with id: {nodeContainertId} Please make sure that events are applied in sequence to the projection.");

            if (!_nodeContainerByInterestId.TryRemove(nodeContainerInterestId, out _))
                throw new ApplicationException($"Concurrency issue removing node container from interest dictionary. Span equipment id: {nodeContainertId} Please make sure that events are applied in sequence to the projection.");
        }

        private void AddTerminalEquipmentToNameIndex(TerminalEquipment terminalEquipment)
        {
            if (terminalEquipment.NamingInfo != null && !String.IsNullOrEmpty(terminalEquipment.NamingInfo.Name))
            {
                if (_terminalEquipmentIdByName.TryGetValue(terminalEquipment.NamingInfo.Name.ToLower(), out var existingIndexEntry))
                {
                     existingIndexEntry.Add(terminalEquipment.Id);
                }
                else
                {
                    _terminalEquipmentIdByName[terminalEquipment.NamingInfo.Name.ToLower()] = new HashSet<Guid> { terminalEquipment.Id };
                }
            }
        }

        private void RemoveTerminalEquipmentFromNameIndex(TerminalEquipment terminalEquipment)
        {
            if (terminalEquipment.NamingInfo != null && !String.IsNullOrEmpty(terminalEquipment.NamingInfo.Name))
            {
                if (_terminalEquipmentIdByName.TryGetValue(terminalEquipment.NamingInfo.Name.ToLower(), out var existingIndexEntry))
                {
                    existingIndexEntry.Remove(terminalEquipment.Id);

                    if (existingIndexEntry.Count == 0)
                        _terminalEquipmentIdByName.Remove(terminalEquipment.NamingInfo.Name.ToLower(), out var _);
                }
            }
        }
    }
}
