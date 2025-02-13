using FluentResults;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.Graph.Projections;
using OpenFTTH.UtilityGraphService.Business.NodeContainers.Events;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers
{
    /// <summary>
    /// The root structure placed in a route network node - i.e. cabinet, building, well, conduit closure etc.
    /// </summary>
    public class NodeContainerAR : AggregateBase
    {
        private NodeContainer? _container;

        public NodeContainerAR()
        {
            Register<NodeContainerPlacedInRouteNetwork>(Apply);
            Register<NodeContainerRemovedFromRouteNetwork>(Apply);
            Register<NodeContainerVerticalAlignmentReversed>(Apply);
            Register<NodeContainerManufacturerChanged>(Apply);
            Register<NodeContainerSpecificationChanged>(Apply);

            Register<NodeContainerRackAdded>(Apply);
            Register<NodeContainerRackRemoved>(Apply);
            Register<NodeContainerRackSpecificationChanged>(Apply);
            Register<NodeContainerRackNameChanged>(Apply);
            Register<NodeContainerRackHeightInUnitsChanged>(Apply);

            Register<NodeContainerTerminalEquipmentAdded>(Apply);
            Register<NodeContainerTerminalEquipmentsAddedToRack>(Apply);
            Register<NodeContainerTerminalEquipmentMovedToRack>(Apply);
            Register<NodeContainerTerminalEquipmentReferenceRemoved>(Apply);
            Register<NodeContainerTerminalsConnected>(Apply);
            Register<NodeContainerTerminalsDisconnected>(Apply);
        }

        #region Place in network

        public Result PlaceNodeContainerInRouteNetworkNode(
            CommandContext cmdContext,
            IReadOnlyDictionary<Guid, NodeContainer> nodeContainers,
            LookupCollection<NodeContainerSpecification> nodeContainerSpecifications,
            Guid nodeContainerId, 
            Guid nodeContainerSpecificationId,
            RouteNetworkInterest nodeOfInterest,
            NamingInfo? namingInfo,
            LifecycleInfo? lifecycleInfo,
            Guid? manufacturerId
        )
        {
            this.Id = nodeContainerId;

            if (nodeContainerId == Guid.Empty)
                return Result.Fail(new NodeContainerError(NodeContainerErrorCodes.INVALID_NODE_CONTAINER_ID_CANNOT_BE_EMPTY, "Node container id cannot be empty. A unique id must be provided by client."));

            if (nodeContainers.ContainsKey(nodeContainerId))
                return Result.Fail(new NodeContainerError(NodeContainerErrorCodes.INVALID_NODE_CONTAINER_ID_ALREADY_EXISTS, $"A node container with id: {nodeContainerId} already exists."));

            if (nodeOfInterest.Kind != RouteNetworkInterestKindEnum.NodeOfInterest)
                return Result.Fail(new NodeContainerError(NodeContainerErrorCodes.INVALID_INTEREST_KIND_MUST_BE_NODE_OF_INTEREST, "Interest kind must be NodeOfInterest. You can only put node container into route nodes!"));

            if (!nodeContainerSpecifications.ContainsKey(nodeContainerSpecificationId))
                return Result.Fail(new NodeContainerError(NodeContainerErrorCodes.INVALID_NODE_CONTAINER_SPECIFICATION_ID_NOT_FOUND, $"Cannot find node container specification with id: {nodeContainerSpecificationId}"));

            if (nodeContainers.Any(n => n.Value.RouteNodeId == nodeOfInterest.RouteNetworkElementRefs[0]))
                return Result.Fail(new NodeContainerError(NodeContainerErrorCodes.NODE_CONTAINER_ALREADY_EXISTS_IN_ROUTE_NODE, $"A node container already exist in the route node with id: {nodeOfInterest.RouteNetworkElementRefs[0]} Only one node container is allowed per route node.")); 

            var nodeContainer = new NodeContainer(nodeContainerId, nodeContainerSpecificationId, nodeOfInterest.Id, nodeOfInterest.RouteNetworkElementRefs[0])
            {
                ManufacturerId = manufacturerId,
                NamingInfo = namingInfo, 
                LifecycleInfo = lifecycleInfo
            };

            var nodeContainerPlaceInRouteNetworkEvent = new NodeContainerPlacedInRouteNetwork(nodeContainer)
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(nodeContainerPlaceInRouteNetworkEvent);

            return Result.Ok();
        }

        private void Apply(NodeContainerPlacedInRouteNetwork obj)
        {
            Id = obj.Container.Id;
            _container = obj.Container;
        }

        #endregion

        #region Remove from network
        public Result Remove(CommandContext cmdContext, List<SpanEquipment> relatedSpanEquipments)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            if (IsAnySpanEquipmentIsAffixedToContainer(relatedSpanEquipments))
            {
                return Result.Fail(new RemoveNodeContainerFromRouteNetworkError(
                    RemoveNodeContainerFromRouteNetworkErrorCodes.CANNOT_REMOVE_NODE_CONTAINER_WITH_AFFIXED_SPAN_EQUIPMENT,
                    $"Cannot remove a node container when span equipment(s) are affixed to it")
                );
            }

            if (IsAnyRacksWithinContainer())
            {
                return Result.Fail(new RemoveNodeContainerFromRouteNetworkError(
                    RemoveNodeContainerFromRouteNetworkErrorCodes.CANNOT_REMOVE_NODE_CONTAINER_CONTAINING_RACKS,
                    $"Cannot remove a node container that contains racks")
                );
            }

            if (IsAnyTerminalEquipmentsWithinContainer())
            {
                return Result.Fail(new RemoveNodeContainerFromRouteNetworkError(
                    RemoveNodeContainerFromRouteNetworkErrorCodes.CANNOT_REMOVE_NODE_CONTAINER_CONTAINING_TERMINAL_EQUIPMENT,
                    $"Cannot remove a node container that contains terminal equipments")
                );
            }


            var @event = new NodeContainerRemovedFromRouteNetwork(
               nodeContainerId: this.Id
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private bool IsAnySpanEquipmentIsAffixedToContainer(List<SpanEquipment> relatedSpanEquipments)
        {
            foreach (var spanEquipment in relatedSpanEquipments)
            {
                if (spanEquipment.NodeContainerAffixes != null)
                {
                    foreach (var affix in spanEquipment.NodeContainerAffixes)
                    {
                        if (affix.NodeContainerId == this.Id)
                            return true;
                    }
                }
            }

            return false;
        }

        private bool IsAnyRacksWithinContainer()
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            if (_container.Racks != null && _container.Racks.Length > 0)
                return true;

            return false;
        }

        private bool IsAnyTerminalEquipmentsWithinContainer()
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            if (_container.TerminalEquipmentReferences != null && _container.TerminalEquipmentReferences.Length > 0)
                return true;

            return false;
        }


        private bool CheckIfTerminalReferenceExistsInContainer(Guid terminalEquipmentId)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            if (_container.TerminalEquipmentReferences != null && _container.TerminalEquipmentReferences.Contains(terminalEquipmentId))
                return true;

            if (_container.Racks != null && _container.Racks.Length > 0)
            {
                foreach (var rack in _container.Racks)
                {
                    foreach (var subrackMount in rack.SubrackMounts)
                    {
                        if (subrackMount.TerminalEquipmentId == terminalEquipmentId)
                            return true;
                    }
                }
            }

            return false;
        }


        private void Apply(NodeContainerRemovedFromRouteNetwork @event)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");
        }

        #endregion

        #region Remove Rack
        public Result RemoveRack(CommandContext cmdContext, Guid rackId)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            if (_container.Racks == null || !_container.Racks.Any(r => r.Id == rackId))
                return Result.Fail(new NodeContainerError(NodeContainerErrorCodes.RACK_ID_NOT_FOUND, $"No rack with id: {rackId} found"));

            var @event = new NodeContainerRackRemoved(
                   nodeContainerId: this.Id,
                   rackId: rackId
                )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private void Apply(NodeContainerRackRemoved @event)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            _container = NodeContainerProjectionFunctions.Apply(_container, @event);
        }

        #endregion

        #region Remove terminal equipment reference
        public Result RemoveTerminalEquipmentReference(CommandContext cmdContext, Guid terminalEquipmentId)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            if (CheckIfTerminalReferenceExistsInContainer(terminalEquipmentId))
            {
                var @event = new NodeContainerTerminalEquipmentReferenceRemoved(
                   nodeContainerId: this.Id,
                   terminalEquipmentId: terminalEquipmentId
                )
                {
                    CorrelationId = cmdContext.CorrelationId,
                    IncitingCmdId = cmdContext.CmdId,
                    UserName = cmdContext.UserContext?.UserName,
                    WorkTaskId = cmdContext.UserContext?.WorkTaskId
                };

                RaiseEvent(@event);
            }

            return Result.Ok();
        }

        private void Apply(NodeContainerTerminalEquipmentReferenceRemoved @event)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            _container = NodeContainerProjectionFunctions.Apply(_container, @event);
        }

        #endregion

        #region Reverse vertical content alignment
        public Result ReverseVerticalContentAlignment(CommandContext cmdContext)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            var reverseEvent = new NodeContainerVerticalAlignmentReversed(this.Id)
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(reverseEvent);

            return Result.Ok();
        }

        private void Apply(NodeContainerVerticalAlignmentReversed @event)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            _container = NodeContainerProjectionFunctions.Apply(_container, @event);
        }

        #endregion

        #region Place Rack

        public Result PlaceRack(CommandContext cmdContext, Guid rackId, Guid rackSpecificationId, string rackName, int? rackPosition, int rackHeightInUnits, LookupCollection<RackSpecification> rackSpecifications)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            if (!rackSpecifications.ContainsKey(rackSpecificationId))
                return Result.Fail(new NodeContainerError(NodeContainerErrorCodes.INVALID_RACK_SPECIFICATION_ID_NOT_FOUND, $"Cannot find rack specification with id: {rackSpecificationId}"));

            if (rackPosition == null)
            {
                rackPosition = GetRackPosition();
            }

            if (ValidateRackNameAndPosition(rackName, rackPosition.Value).Errors.FirstOrDefault() is Error error)
                return Result.Fail(error);


            var @event = new NodeContainerRackAdded(
                nodeContainerId: this.Id,
                rackId: rackId,
                rackSpecificationId: rackSpecificationId,
                rackName: rackName,
                rackPosition: rackPosition.Value,
                rackHeightInUnits: rackHeightInUnits
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private int GetRackPosition()
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            if (_container.Racks == null)
                return 1;

            int maxRackPos = 0;

            foreach (var rack in _container.Racks)
            {
                if (rack.Position > maxRackPos)
                    maxRackPos = rack.Position;
            }

            return maxRackPos + 1;
        }

        private void Apply(NodeContainerRackAdded @event)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            _container = NodeContainerProjectionFunctions.Apply(_container, @event);
        }

        private Result ValidateRackNameAndPosition(string rackName, int position)
        {
            if (String.IsNullOrEmpty(rackName))
                return Result.Fail(new NodeContainerError(NodeContainerErrorCodes.INVALID_RACK_NAME_NOT_SPECIFIED, "Rack name is mandatory"));

            if (_container != null && _container.Racks != null && _container.Racks.Any(r => r.Name.ToLower() == rackName.ToLower()))
                return Result.Fail(new NodeContainerError(NodeContainerErrorCodes.INVALID_RACK_NAME_NOT_UNIQUE, $"Rack name: '{rackName}' already used in node container with id: {this.Id}"));

            if (_container != null && _container.Racks != null && _container.Racks.Any(r => r.Position == position))
                return Result.Fail(new NodeContainerError(NodeContainerErrorCodes.INVALID_RACK_POSITION_NOT_UNIQUE, $"Rack position: {position} already used in node container with id: {this.Id}"));

            return Result.Ok();
        }

        #endregion

        #region Add Terminal Equipment To Node
        public Result AddTerminalEquipmentToNode(CommandContext cmdContext, Guid terminalEquipmentId)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            var e = new NodeContainerTerminalEquipmentAdded(this.Id, terminalEquipmentId)
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(e);

            return Result.Ok();
        }

        private void Apply(NodeContainerTerminalEquipmentAdded @event)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            _container = NodeContainerProjectionFunctions.Apply(_container, @event);
        }

        #endregion

        #region Add Terminal Equipments to Rack
        public Result AddTerminalEquipmentsToRack(CommandContext cmdContext, Guid[] terminalEquipmentIds, TerminalEquipmentSpecification terminalEquipmentSpecification, Guid rackId, int startUnitPosition, SubrackPlacmentMethod placmentMethod)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            if (_container.Racks == null || !_container.Racks.Any(r => r.Id == rackId))
                return Result.Fail(new TerminalEquipmentError(TerminalEquipmentErrorCodes.RACK_NOT_FOUND, $"Cannot find rack with id: {rackId} in node container with id: {this.Id}"));

            if (startUnitPosition < 0)
                return Result.Fail(new TerminalEquipmentError(TerminalEquipmentErrorCodes.INVALID_RACK_UNIT_START_POSITION, $"Invalid rack unit must be greater or equal to zero"));

            var rack = _container.Racks.First(r => r.Id == rackId);

            // Check that there is space where the equipment(s) are inserted
            var nEquipment = 0;

            foreach (var equipmentIdToInsert in terminalEquipmentIds)
            {
                var position = startUnitPosition + (nEquipment * terminalEquipmentSpecification.HeightInRackUnits);

                if (!TerminalEquipmentFitsInRack(rackId, equipmentIdToInsert, position, terminalEquipmentSpecification.HeightInRackUnits))
                    return Result.Fail(new TerminalEquipmentError(TerminalEquipmentErrorCodes.TERMINAL_EQUIPMENT_DOES_NOT_FIT_IN_RACK, $"The terminal equipment with id {equipmentIdToInsert} cannot be inserted in rack: {rackId} position: {position} because there's no free space in rack."));

                nEquipment++;
            }


            var revisedStartPoistion = ReviseStartPosition(rack, startUnitPosition);

            var orderedTerminalEquipmentIds = terminalEquipmentIds;

            // change order depending placement method
            bool reverse = false;

            if (placmentMethod == SubrackPlacmentMethod.TopDown)
                reverse = true;

            var e = new NodeContainerTerminalEquipmentsAddedToRack(
                nodeContainerId: this.Id,
                rackId: rackId,
                startUnitPosition: revisedStartPoistion,
                terminalEquipmentIds: reverse ? terminalEquipmentIds.Reverse().ToArray() : terminalEquipmentIds,
                terminalEquipmentHeightInUnits: terminalEquipmentSpecification.HeightInRackUnits
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(e);

            return Result.Ok();
        }

        private void Apply(NodeContainerTerminalEquipmentsAddedToRack @event)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            _container = NodeContainerProjectionFunctions.Apply(_container, @event);
        }

        private int ReviseStartPosition(Rack rack, int startUnitPosition)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            foreach (var subrackMount in rack.SubrackMounts)
            {
                // If start position is within an exisiting equipment move to position where that equipment sits
                if (startUnitPosition > subrackMount.Position && startUnitPosition < (subrackMount.Position + subrackMount.HeightInUnits))
                    return subrackMount.Position;
            }

            return startUnitPosition;
        }

        #endregion

        #region Move Rack Equipment
        public Result MoveRackEquipment(CommandContext cmdContext, Guid terminalEquipmentId, TerminalEquipmentSpecification terminalEquipmentSpecification, Guid moveToRackId, int moveToRackPosition)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            if (_container.Racks == null || !_container.Racks.Any(r => r.Id == moveToRackId))
                return Result.Fail(new TerminalEquipmentError(TerminalEquipmentErrorCodes.RACK_NOT_FOUND, $"Cannot find rack with id: {moveToRackId} in node container with id: {this.Id}"));

            if (!TerminalEquipmentExistInRack(terminalEquipmentId))
                return Result.Fail(new TerminalEquipmentError(TerminalEquipmentErrorCodes.TERMINAL_EQUIPMENT_NOT_FOUND_IN_ANY_RACK, $"Cannot find terminal equipment with id {terminalEquipmentId} in any rack within node container with id: {this.Id}"));

            // Check that there is space where the equipment is moved to
            if (!TerminalEquipmentFitsInRack(moveToRackId, terminalEquipmentId, moveToRackPosition, terminalEquipmentSpecification.HeightInRackUnits))
                return Result.Fail(new TerminalEquipmentError(TerminalEquipmentErrorCodes.TERMINAL_EQUIPMENT_DOES_NOT_FIT_IN_RACK, $"The terminal equipment with id {terminalEquipmentId} cannot be moved to rack: {moveToRackId} position: {moveToRackPosition} because there's no free space in rack."));

            var e = new NodeContainerTerminalEquipmentMovedToRack(
                  nodeContainerId: this.Id,
                  oldRackId: GetCurrentRack(terminalEquipmentId).Id,
                  newRackId: moveToRackId,
                  startUnitPosition: moveToRackPosition,
                  terminalEquipmentId: terminalEquipmentId,
                  terminalEquipmentHeightInUnits: terminalEquipmentSpecification.HeightInRackUnits
              )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(e);

            return Result.Ok();
        }

        private bool TerminalEquipmentFitsInRack(Guid rackId, Guid terminalEquipmentId, int rackPosition, int heightInRackUnits)
        {
            var rack = _container.Racks.First(r => r.Id == rackId);

            HashSet<int> usedPositions = new();

            foreach (var subRack in rack.SubrackMounts)
            {
                for (int usedPosition = subRack.Position; usedPosition < (subRack.Position + subRack.HeightInUnits); usedPosition++)
                {
                    if (subRack.TerminalEquipmentId != terminalEquipmentId)
                        usedPositions.Add(usedPosition);
                }
            }

            for (int position = rackPosition; position < (rackPosition + heightInRackUnits); position++)
            {
                if (usedPositions.Contains(position))
                    return false;
            }

            return true;
        }

        private bool TerminalEquipmentCanBeMovedDown(Guid terminalEquipmentId, int moveDownUnits)
        {
            var rack = GetCurrentRack(terminalEquipmentId);

            var subrackToMove = rack.SubrackMounts.First(s => s.TerminalEquipmentId == terminalEquipmentId);

            HashSet<int> usedPositions = new();

            foreach (var subRack in rack.SubrackMounts)
            {
                for (int usedPosition = subRack.Position; usedPosition < (subRack.Position + subRack.HeightInUnits); usedPosition++)
                {
                    if (subRack.TerminalEquipmentId != terminalEquipmentId)
                        usedPositions.Add(usedPosition);
                }
            }

            for (int position = subrackToMove.Position - moveDownUnits; position <= (subrackToMove.Position - moveDownUnits) + subrackToMove.HeightInUnits; position++)
            {
                if (usedPositions.Contains(position))
                    return false;

                if (position < 0)
                    return false;
            }

            return true;
        }

        private void Apply(NodeContainerTerminalEquipmentMovedToRack @event)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            _container = NodeContainerProjectionFunctions.Apply(_container, @event);
        }

        private bool CheckIfMovedToNewRack(Guid equipmentId, Guid possibleNewRackId)
        {
            var currentRackId = GetCurrentRack(equipmentId).Id;

            if (currentRackId != possibleNewRackId)
                return true;
            else
                return false;
        }

        private bool TerminalEquipmentExistInRack(Guid terminalEquipmentId)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            if (_container.Racks == null)
                return false;

            foreach (var rack in _container.Racks)
            {
                foreach (var subrackMount in rack.SubrackMounts)
                {
                    if (subrackMount.TerminalEquipmentId == terminalEquipmentId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private Rack GetCurrentRack(Guid terminalEquipmentId)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            if (_container.Racks == null)
                throw new ApplicationException($"No racks in node equipment with id: {_container.Id}");

            foreach (var rack in _container.Racks)
            {
                foreach (var subrackMount in rack.SubrackMounts)
                {
                    if (subrackMount.TerminalEquipmentId == terminalEquipmentId)
                    {
                        return rack;
                    }
                }
            }

            throw new ApplicationException($"Can't find terminal equipment with id: {terminalEquipmentId} in any racks.");
        }

        #endregion

        #region Arrange Rack Equipment
        internal Result ArrangeRackEquipment(CommandContext cmdContext, Guid terminalEquipmentId, RackEquipmentArrangeMethodEnum arrangeMethod, int numberOfRackPositions)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            if (!TerminalEquipmentExistInRack(terminalEquipmentId))
                return Result.Fail(new TerminalEquipmentError(TerminalEquipmentErrorCodes.TERMINAL_EQUIPMENT_NOT_FOUND_IN_ANY_RACK, $"Cannot find terminal equipment with id {terminalEquipmentId} in any rack within node container with id: {this.Id}"));

            var rack = GetCurrentRack(terminalEquipmentId);

            var terminalEquipmentRackMount = rack.SubrackMounts.First(s => s.TerminalEquipmentId == terminalEquipmentId);

            // Move up has no rule, everything is just moved up
            if (arrangeMethod == RackEquipmentArrangeMethodEnum.MoveUp)
            {
                foreach (var subrack in rack.SubrackMounts)
                {
                    if (subrack.TerminalEquipmentId == terminalEquipmentId || subrack.Position > terminalEquipmentRackMount.Position)
                    {
                        var e = new NodeContainerTerminalEquipmentMovedToRack(
                               nodeContainerId: this.Id,
                               oldRackId: rack.Id,
                               newRackId: rack.Id,
                               startUnitPosition: subrack.Position + numberOfRackPositions,
                               terminalEquipmentId: subrack.TerminalEquipmentId,
                               terminalEquipmentHeightInUnits: subrack.HeightInUnits
                           )
                        {
                            CorrelationId = cmdContext.CorrelationId,
                            IncitingCmdId = cmdContext.CmdId,
                            UserName = cmdContext.UserContext?.UserName,
                            WorkTaskId = cmdContext.UserContext?.WorkTaskId
                        };

                        RaiseEvent(e);
                    }

                }

            }
            else
            {
                // Check that there is space where the equipment is moved to
                if (!TerminalEquipmentCanBeMovedDown(terminalEquipmentId, numberOfRackPositions))
                    return Result.Fail(new TerminalEquipmentError(TerminalEquipmentErrorCodes.TERMINAL_EQUIPMENT_CANNOT_BE_MOVED_DOWN_DUE_TO_LACK_OF_FREE_SPACE, $"The terminal equipment with id {terminalEquipmentId} cannot be moved down {numberOfRackPositions}, because there's no free space in rack."));

                // Move everything down
                foreach (var subrack in rack.SubrackMounts)
                {
                    if (subrack.TerminalEquipmentId == terminalEquipmentId || subrack.Position > terminalEquipmentRackMount.Position)
                    {
                        var e = new NodeContainerTerminalEquipmentMovedToRack(
                               nodeContainerId: this.Id,
                               oldRackId: rack.Id,
                               newRackId: rack.Id,
                               startUnitPosition: subrack.Position - numberOfRackPositions,
                               terminalEquipmentId: subrack.TerminalEquipmentId,
                               terminalEquipmentHeightInUnits: subrack.HeightInUnits
                           )
                        {
                            CorrelationId = cmdContext.CorrelationId,
                            IncitingCmdId = cmdContext.CmdId,
                            UserName = cmdContext.UserContext?.UserName,
                            WorkTaskId = cmdContext.UserContext?.WorkTaskId
                        };

                        RaiseEvent(e);
                    }

                }
            }

            return Result.Ok();

        }
        #endregion

        #region Connect Terminals
        public Result ConnectTerminals(CommandContext cmdContext, UtilityGraph graph, TerminalEquipment fromTerminalEquipment, Guid fromTerminalId, TerminalEquipment toTerminalEquipment, Guid toTerminalId, double fiberCoordLength)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            if (!CheckIfTerminalReferenceExistsInContainer(fromTerminalEquipment.Id))
                throw new ApplicationException($"Terminal equipment with id: {fromTerminalEquipment.Id} not found in node container with id: {this.Id}");

            if (!CheckIfTerminalReferenceExistsInContainer(toTerminalEquipment.Id))
                throw new ApplicationException($"Terminal equipment with id: {toTerminalEquipment.Id} not found in node container with id: {this.Id}");
            
            if (!CheckIfTerminalExistsInEquipment(fromTerminalEquipment, fromTerminalId))
                throw new ApplicationException($"Terminal with id: {fromTerminalId} not found in terminal equipment with id: {fromTerminalEquipment.Id} Error trying to connect terminals in node container with id: {this.Id}");

            if (!CheckIfTerminalExistsInEquipment(toTerminalEquipment, toTerminalId))
                throw new ApplicationException($"Terminal with id: {toTerminalId} not found in terminal equipment with id: {toTerminalEquipment.Id} Error trying to connect terminals in node container with id: {this.Id}");
        
            if (fromTerminalId == toTerminalId)
                throw new ApplicationException($"Terminal with id: {toTerminalId} can't be connected to itself. Error trying to connect terminals in node container with id: {this.Id}");

            if (IsTerminalFullyConnected(graph, fromTerminalEquipment, fromTerminalId))
                return Result.Fail(new ConnectTerminalsAtRouteNodeError(ConnectTerminalsAtRouteNodeErrorCodes.TERMINAL_ALREADY_CONNECTED, $"The terminal with id: {fromTerminalId} in terminal equipment with id: {fromTerminalEquipment.Id} is allready fully connected"));

            if (IsTerminalFullyConnected(graph, toTerminalEquipment, toTerminalId))
                return Result.Fail(new ConnectTerminalsAtRouteNodeError(ConnectTerminalsAtRouteNodeErrorCodes.TERMINAL_ALREADY_CONNECTED, $"The terminal with id: {toTerminalId} in terminal equipment with id: {toTerminalEquipment.Id} is allready fully connected"));

            var e = new NodeContainerTerminalsConnected(
                  connectionId: Guid.NewGuid(),
                  nodeContainerId: this.Id,
                  fromTerminalEquipmentId: fromTerminalEquipment.Id,
                  fromTerminalId: fromTerminalId,
                  toTerminalEquipmentId: toTerminalEquipment.Id,
                  toTerminalId: toTerminalId,
                  fiberCoordLength: fiberCoordLength
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(e);

            return Result.Ok();

        }

        private void Apply(NodeContainerTerminalsConnected @event)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            _container = NodeContainerProjectionFunctions.Apply(_container, @event);
        }

        private bool CheckIfTerminalExistsInEquipment(TerminalEquipment fromTerminalEquipment, Guid fromTerminalId)
        {
            foreach (var terminalStructure in fromTerminalEquipment.TerminalStructures.Where(t => !t.Deleted))
            {
                foreach (var terminal in terminalStructure.Terminals)
                {
                    if (terminal.Id == fromTerminalId)
                        return true;
                }
            }

            return false;
        }


        private bool IsTerminalFullyConnected(UtilityGraph graph, TerminalEquipment _terminalEquipment, Guid terminalId)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");
          
            var version = graph.LatestCommitedVersion;

            foreach (var terminalStructure in _terminalEquipment.TerminalStructures)
            {
                foreach (var terminal in terminalStructure.Terminals)
                {
                    if (terminal.Id == terminalId)
                    {
                        var terminalElement = graph.GetTerminal(terminal.Id, version);

                        if (terminalElement != null && terminalElement is UtilityGraphConnectedTerminal connectedTerminal)
                        {
                            if (terminal.Direction == TerminalDirectionEnum.BI)
                            {
                                if (connectedTerminal.NeighborElements(version).Count > 1)
                                    return true;
                            }
                            else
                            {
                                if (connectedTerminal.NeighborElements(version).Where(n => !(n is UtilityGraphInternalEquipmentConnectivityLink)).Count() > 0)
                                    return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private bool IsTerminalConnected(UtilityGraph graph, TerminalEquipment _terminalEquipment, Guid terminalId)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            var version = graph.LatestCommitedVersion;

            foreach (var terminalStructure in _terminalEquipment.TerminalStructures)
            {
                foreach (var terminal in terminalStructure.Terminals)
                {
                    if (terminal.Id == terminalId)
                    {
                        var terminalElement = graph.GetTerminal(terminal.Id, version);

                        if (terminalElement != null && terminalElement is UtilityGraphConnectedTerminal connectedTerminal)
                        {
                            if (terminal.Direction == TerminalDirectionEnum.BI)
                            {
                                if (connectedTerminal.NeighborElements(version).Count > 0)
                                    return true;
                            }
                            else
                            {
                                if (connectedTerminal.NeighborElements(version).Where(n => !(n is UtilityGraphInternalEquipmentConnectivityLink)).Count() > 0)
                                    return true;
                            }
                        }
                    }
                }
            }

            return false;
        }


        #endregion

        #region Disconnect Terminals
        public Result DisconnectTerminals(CommandContext cmdContext, UtilityGraph graph, TerminalEquipment fromTerminalEquipment, Guid fromTerminalId, TerminalEquipment toTerminalEquipment, Guid toTerminalId)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            if (!CheckIfTerminalReferenceExistsInContainer(fromTerminalEquipment.Id))
                throw new ApplicationException($"Terminal equipment with id: {fromTerminalEquipment.Id} not found in node container with id: {this.Id}");

            if (!CheckIfTerminalReferenceExistsInContainer(toTerminalEquipment.Id))
                throw new ApplicationException($"Terminal equipment with id: {toTerminalEquipment.Id} not found in node container with id: {this.Id}");

            if (!CheckIfTerminalExistsInEquipment(fromTerminalEquipment, fromTerminalId))
                throw new ApplicationException($"Terminal with id: {fromTerminalId} not found in terminal equipment with id: {fromTerminalEquipment.Id} Error trying to disconnect terminals in node container with id: {this.Id}");

            if (!CheckIfTerminalExistsInEquipment(toTerminalEquipment, toTerminalId))
                throw new ApplicationException($"Terminal with id: {toTerminalId} not found in terminal equipment with id: {toTerminalEquipment.Id} Error trying to disconnect terminals in node container with id: {this.Id}");

            if (!IsTerminalConnected(graph, fromTerminalEquipment, fromTerminalId))
                return Result.Fail(new ConnectTerminalsAtRouteNodeError(ConnectTerminalsAtRouteNodeErrorCodes.TERMINAL_NOT_CONNECTED, $"The terminal with id: {fromTerminalId} in terminal equipment with id: {fromTerminalEquipment.Id} is not connected"));

            if (!IsTerminalConnected(graph, toTerminalEquipment, toTerminalId))
                return Result.Fail(new ConnectTerminalsAtRouteNodeError(ConnectTerminalsAtRouteNodeErrorCodes.TERMINAL_NOT_CONNECTED, $"The terminal with id: {toTerminalId} in terminal equipment with id: {toTerminalEquipment.Id} is not connected"));

            var e = new NodeContainerTerminalsDisconnected(
                  nodeContainerId: this.Id,
                  fromTerminalEquipmentId: fromTerminalEquipment.Id,
                  fromTerminalId: fromTerminalId,
                  toTerminalEquipmentId: toTerminalEquipment.Id,
                  toTerminalId: toTerminalId
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(e);

            return Result.Ok();
        }

        private void Apply(NodeContainerTerminalsDisconnected @event)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            _container = NodeContainerProjectionFunctions.Apply(_container, @event);
        }
       

        #endregion

        #region Change Manufacturer
        public Result ChangeManufacturer(CommandContext cmdContext, Guid manufacturerId)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            if (_container.ManufacturerId == manufacturerId)
            {
                return Result.Fail(new UpdateNodeContainerPropertiesError(
                       UpdateNodeContainerPropertiesErrorCodes.NO_CHANGE_TO_MANUFACTURER,
                       $"Will not change manufacturer, because the provided value is equal the existing value.")
                   );
            }

            var @event = new NodeContainerManufacturerChanged(
              nodeContainerId: this.Id,
              manufacturerId: manufacturerId
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private void Apply(NodeContainerManufacturerChanged @event)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            _container = NodeContainerProjectionFunctions.Apply(_container, @event);
        }

        #endregion

        #region Change Specification
        public Result ChangeSpecification(CommandContext cmdContext, NodeContainerSpecification currentSpecification, NodeContainerSpecification newSpecification)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            if (_container.SpecificationId == newSpecification.Id)
            {
                return Result.Fail(new UpdateNodeContainerPropertiesError(
                       UpdateNodeContainerPropertiesErrorCodes.NO_CHANGE_TO_SPECIFICATION,
                       $"Will not change specification, because the provided specification id is the same as the existing one.")
                   );
            }


            var @event = new NodeContainerSpecificationChanged(
              nodeContainerId: this.Id,
              newSpecificationId: newSpecification.Id)
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private void Apply(NodeContainerSpecificationChanged @event)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            _container = NodeContainerProjectionFunctions.Apply(_container, @event);
        }

        #endregion

        #region Rack specification changed
        public Result ChangeRackSpecification(CommandContext cmdContext, Guid rackId, RackSpecification oldRackSpecification, RackSpecification newRackSpecification)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            if (_container.Racks == null || !_container.Racks.Any(r => r.Id == rackId))
                return Result.Fail(new UpdateNodeContainerPropertiesError(UpdateNodeContainerPropertiesErrorCodes.RACK_NOT_FOUND, $"Cannot find rack with id: {rackId} in node container with id: {_container.Id}"));

            var rack = _container.Racks.First(r => r.Id == rackId);


            if (rack.SpecificationId == newRackSpecification.Id)
            {
                return Result.Fail(new UpdateNodeContainerPropertiesError(
                       UpdateNodeContainerPropertiesErrorCodes.NO_CHANGE_TO_SPECIFICATION,
                       $"Will not change specification on rack with id: {rackId} because the provided rack specification id is the same as the existing one.")
                   );
            }


            var @event = new NodeContainerRackSpecificationChanged(
              nodeContainerId: this.Id,
              rackId: rack.Id,
              newSpecificationId: newRackSpecification.Id)
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private void Apply(NodeContainerRackSpecificationChanged @event)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            _container = NodeContainerProjectionFunctions.Apply(_container, @event);
        }


        #endregion

        #region Rack name changed
        public Result ChangeRackName(CommandContext cmdContext, Guid rackId, string rackName)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            if (_container.Racks == null || !_container.Racks.Any(r => r.Id == rackId))
                return Result.Fail(new UpdateNodeContainerPropertiesError(UpdateNodeContainerPropertiesErrorCodes.RACK_NOT_FOUND, $"Cannot find rack with id: {rackId} in node container with id: {_container.Id}"));

            var rack = _container.Racks.First(r => r.Id == rackId);


            if (rack.Name == rackName)
            {
                return Result.Fail(new UpdateNodeContainerPropertiesError(
                       UpdateNodeContainerPropertiesErrorCodes.NO_CHANGE_TO_NAME,
                       $"Will not change name on rack with id: {rackId} because the provided rack name is the same as the existing one.")
                   );
            }


            var @event = new NodeContainerRackNameChanged(
              nodeContainerId: this.Id,
              rackId: rack.Id,
              newName: rackName)
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private void Apply(NodeContainerRackNameChanged @event)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            _container = NodeContainerProjectionFunctions.Apply(_container, @event);
        }

        #endregion

        #region Rack height in units changed
        public Result ChangeRackHeightInUnits(CommandContext cmdContext, Guid rackId, int heightInUnits)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            if (_container.Racks == null || !_container.Racks.Any(r => r.Id == rackId))
                return Result.Fail(new UpdateNodeContainerPropertiesError(UpdateNodeContainerPropertiesErrorCodes.RACK_NOT_FOUND, $"Cannot find rack with id: {rackId} in node container with id: {_container.Id}"));

            var rack = _container.Racks.First(r => r.Id == rackId);


            if (rack.HeightInUnits == heightInUnits)
            {
                return Result.Fail(new UpdateNodeContainerPropertiesError(
                       UpdateNodeContainerPropertiesErrorCodes.NO_CHANGE_TO_HEIGHT,
                       $"Will not change height of rack with id: {rackId} because the provided height is the same as the existing one.")
                   );
            }


            var @event = new NodeContainerRackHeightInUnitsChanged(
              nodeContainerId: this.Id,
              rackId: rack.Id,
              newHeightInUnits: heightInUnits)
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private void Apply(NodeContainerRackHeightInUnitsChanged @event)
        {
            if (_container == null)
                throw new ApplicationException($"Invalid internal state. Node container property cannot be null. Seems that node container has never been created. Please check command handler logic.");

            _container = NodeContainerProjectionFunctions.Apply(_container, @event);
        }

        #endregion
              

      

        
    }
}
