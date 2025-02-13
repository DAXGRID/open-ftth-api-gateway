using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.NodeContainers.Events;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.UtilityGraphService.Business.Graph.Projections
{
    public static class NodeContainerProjectionFunctions
    {
        public static NodeContainer Apply(NodeContainer existingSpanEquipment, NodeContainerVerticalAlignmentReversed alignmentReversed)
        {
            var newAllignment = existingSpanEquipment.VertialContentAlignmemt == NodeContainerVerticalContentAlignmentEnum.Bottom ? NodeContainerVerticalContentAlignmentEnum.Top : NodeContainerVerticalContentAlignmentEnum.Bottom;

            return existingSpanEquipment with
            {
                VertialContentAlignmemt = newAllignment
            };
        }

        public static NodeContainer Apply(NodeContainer existingEquipment, NodeContainerManufacturerChanged @event)
        {
            return existingEquipment with
            {
                ManufacturerId = @event.ManufacturerId
            };
        }

        public static NodeContainer Apply(NodeContainer existingEquipment, NodeContainerSpecificationChanged @event)
        {
            return existingEquipment with
            {
                SpecificationId = @event.NewSpecificationId,
            };
        }

        public static NodeContainer Apply(NodeContainer existingEquipment, NodeContainerRackAdded @event)
        {
            List<Rack> newRackList = new();

            if (existingEquipment.Racks != null)
                newRackList.AddRange(existingEquipment.Racks);

            newRackList.Add(new Rack(@event.RackId, @event.RackName, @event.RackPosition, @event.RackSpecificationId, @event.RackHeightInUnits, new SubrackMount[] { }));

            return existingEquipment with
            {
                Racks = newRackList.ToArray()
            };
        }

        public static NodeContainer Apply(NodeContainer existingEquipment, NodeContainerRackRemoved @event)
        {
            List<Rack> newRackList = new();

            if (existingEquipment.Racks != null)
            {
                foreach (var rack in existingEquipment.Racks)
                {
                    if (rack.Id != @event.RackId)
                        newRackList.Add(rack);
                }
            }

            return existingEquipment with
            {
                Racks = newRackList.ToArray()
            };
        }


        public static NodeContainer Apply(NodeContainer existingEquipment, NodeContainerTerminalEquipmentAdded @event)
        {
            List<Guid> newTerminalEquipmentRefList = new();

            if (existingEquipment.TerminalEquipmentReferences != null)
                newTerminalEquipmentRefList.AddRange(existingEquipment.TerminalEquipmentReferences);

            newTerminalEquipmentRefList.Add(@event.TerminalEquipmentId);

            return existingEquipment with
            {
                TerminalEquipmentReferences = newTerminalEquipmentRefList.ToArray()
            };
        }

        public static NodeContainer Apply(NodeContainer existingEquipment, NodeContainerTerminalEquipmentsAddedToRack @event)
        {
            if (existingEquipment.Racks == null)
                return existingEquipment;

            Rack? rack = existingEquipment.Racks.FirstOrDefault(r => r.Id == @event.RackId);

            if (rack == null)
                return existingEquipment;

            int totalHeight = @event.TerminalEquipmentHeightInUnits * @event.TerminalEquipmentIds.Count();

            List<SubrackMount> keepList = new();
            List<SubrackMount> moveUpList = new();

            bool foundFirstEquipmentWithinBlock = false;
            int moveUpUnits = 0;

            foreach (var existingSubrackMount in rack.SubrackMounts.OrderBy(s => s.Position))
            {
                // Check if existing mount found within new equipment(s) block
                if (!foundFirstEquipmentWithinBlock && existingSubrackMount.Position >= @event.StartUnitPosition && existingSubrackMount.Position < (@event.StartUnitPosition + totalHeight))
                {
                    foundFirstEquipmentWithinBlock = true;
                    moveUpUnits = totalHeight - (existingSubrackMount.Position - @event.StartUnitPosition);
                }

                if (foundFirstEquipmentWithinBlock)
                {
                    // We're going to move it up
                    moveUpList.Add(existingSubrackMount with { Position = existingSubrackMount.Position + moveUpUnits });
                }
                else
                {
                    // We keep the position
                    keepList.Add(existingSubrackMount);
                }
            }

            // Add the new terminal equipments to rack
            int insertPosition = @event.StartUnitPosition;

            foreach (var equipmentId in @event.TerminalEquipmentIds)
            {
                keepList.Add(new SubrackMount(equipmentId, insertPosition, @event.TerminalEquipmentHeightInUnits));
                insertPosition += @event.TerminalEquipmentHeightInUnits;
            }

            // Add the moved up terminal equipments
            keepList.AddRange(moveUpList);

            Rack[] newRacks = new Rack[existingEquipment.Racks.Length];

            existingEquipment.Racks.CopyTo(newRacks, 0);

            newRacks[Array.IndexOf(existingEquipment.Racks, rack)] = new Rack(rack.Id, rack.Name, rack.Position, rack.SpecificationId, rack.HeightInUnits, keepList.ToArray());

            return existingEquipment with
            {
                Racks = newRacks
            };
        }

        public static NodeContainer Apply(NodeContainer existingEquipment, NodeContainerRackSpecificationChanged @event)
        {
            if (existingEquipment.Racks == null)
                return existingEquipment;

            List<Rack> newRackList = new List<Rack>();

            foreach (var rack in existingEquipment.Racks)
            {
                if (rack.Id == @event.RackId)
                {
                    newRackList.Add(rack with { SpecificationId = @event.NewSpecificationId });
                }
                else
                {
                    newRackList.Add(rack);
                }
            }

            return existingEquipment with
            {
                Racks = newRackList.ToArray()
            };
        }

        public static NodeContainer Apply(NodeContainer existingEquipment, NodeContainerRackNameChanged @event)
        {
            if (existingEquipment.Racks == null)
                return existingEquipment;

            List<Rack> newRackList = new List<Rack>();

            foreach (var rack in existingEquipment.Racks)
            {
                if (rack.Id == @event.RackId)
                {
                    newRackList.Add(rack with { Name = @event.NewName });
                }
                else
                {
                    newRackList.Add(rack);
                }
            }

            return existingEquipment with
            {
                Racks = newRackList.ToArray()
            };
        }

        public static NodeContainer Apply(NodeContainer existingEquipment, NodeContainerRackHeightInUnitsChanged @event)
        {
            if (existingEquipment.Racks == null)
                return existingEquipment;

            List<Rack> newRackList = new List<Rack>();

            foreach (var rack in existingEquipment.Racks)
            {
                if (rack.Id == @event.RackId)
                {
                    newRackList.Add(rack with { HeightInUnits = @event.NewHeightInUnits });
                }
                else
                {
                    newRackList.Add(rack);
                }
            }

            return existingEquipment with
            {
                Racks = newRackList.ToArray()
            };
        }

        public static NodeContainer Apply(NodeContainer existingEquipment, NodeContainerTerminalEquipmentReferenceRemoved @event)
        {
            bool terminalReferenceFound = false;

            List<Guid> newTerminalEquipmentReferenceList = new();

            if (existingEquipment.TerminalEquipmentReferences != null && existingEquipment.TerminalEquipmentReferences.Length > 0)
            {
                foreach (var terminalRef in existingEquipment.TerminalEquipmentReferences)
                {
                    if (terminalRef != @event.TerminalEquipmentId)
                    {
                        newTerminalEquipmentReferenceList.Add(terminalRef);
                    }
                    else
                    {
                        terminalReferenceFound = true;
                    }
                }
            }

            if (terminalReferenceFound)
            {
                return existingEquipment with
                {
                    TerminalEquipmentReferences = newTerminalEquipmentReferenceList.ToArray()
                };

            }

            // Try find equipment id in racks
            List<Rack> newRackList = new List<Rack>();

            bool rackTerminalReferenceFound = false;

            if (existingEquipment.Racks != null && existingEquipment.Racks.Length > 0)
            {
                foreach (var rack in existingEquipment.Racks)
                {
                    List<SubrackMount> newSubrackList = new();

                    bool subrackMountFound = true;

                    foreach (var subrackMount in rack.SubrackMounts)
                    {
                        if (subrackMount.TerminalEquipmentId != @event.TerminalEquipmentId)
                        {
                            newSubrackList.Add(subrackMount);
                        }
                        else
                        {
                            subrackMountFound = true;
                        }
                    }

                    if (subrackMountFound)
                    {
                        newRackList.Add(rack with { SubrackMounts = newSubrackList.ToArray() });
                        rackTerminalReferenceFound = true;
                    }
                    else
                    {
                        newRackList.Add(rack);
                    }
                }
            }

            if (rackTerminalReferenceFound)
            {
                return existingEquipment with
                {
                    Racks = newRackList.ToArray()
                };
            }
            else
            {
                return existingEquipment;
            }
        }

        public static NodeContainer Apply(NodeContainer existingEquipment, NodeContainerTerminalEquipmentMovedToRack @event)
        {

            // If equipment is moved to another rack
            if (@event.OldRackId != @event.NewRackId)
            {
                // Built new list of subracks in rack from where equipment is move away from
                List<SubrackMount> newMoveFromRackSubrackList = new();

                foreach (var subRack in existingEquipment.Racks.First(r => r.Id == @event.OldRackId).SubrackMounts)
                {
                    if (subRack.TerminalEquipmentId != @event.TerminalEquipmentId)
                        newMoveFromRackSubrackList.Add(subRack);
                }

                // Built new list of subracks in rack from where equipment is move to
                List<SubrackMount> newMoveToRackSubrackList = new();

                newMoveToRackSubrackList.AddRange(existingEquipment.Racks.First(r => r.Id == @event.NewRackId).SubrackMounts);

                newMoveToRackSubrackList.Add(
                    new SubrackMount(
                        @event.TerminalEquipmentId,
                        @event.StartUnitPosition,
                        @event.TerminalEquipmentHeightInUnits
                    )
                );

                List<Rack> newRackList = new List<Rack>();

                foreach (var rack in existingEquipment.Racks)
                {
                    if (rack.Id == @event.OldRackId)
                    {
                        newRackList.Add(rack with { SubrackMounts = newMoveFromRackSubrackList.ToArray() });
                    }
                    else if (rack.Id == @event.NewRackId)
                    {
                        newRackList.Add(rack with { SubrackMounts = newMoveToRackSubrackList.ToArray() });
                    }
                    else
                    {
                        newRackList.Add(rack);
                    }
                }

                return existingEquipment with
                {
                    Racks = newRackList.ToArray()
                };
            }
            else
            {
                // Built new list of subracks in rack from where equipment is move away from
                List<SubrackMount> newSubrackList = new();

                foreach (var subRack in existingEquipment.Racks.First(r => r.Id == @event.OldRackId).SubrackMounts)
                {
                    if (subRack.TerminalEquipmentId == @event.TerminalEquipmentId)
                    {
                        newSubrackList.Add(subRack with { Position = @event.StartUnitPosition });
                    }
                    else
                    {
                        newSubrackList.Add(subRack);
                    }
                }

                List<Rack> newRackList = new List<Rack>();

                foreach (var rack in existingEquipment.Racks)
                {
                    if (rack.Id == @event.OldRackId)
                    {
                        newRackList.Add(rack with { SubrackMounts = newSubrackList.ToArray() });
                    }
                    else
                    {
                        newRackList.Add(rack);
                    }
                }

                return existingEquipment with
                {
                    Racks = newRackList.ToArray()
                };
            }
        }


        public static NodeContainer Apply(NodeContainer existingEquipment, NodeContainerTerminalsConnected @event)
        {
            List<TerminalToTerminalConnection> newTerminalConnectionsList = new();

            if (existingEquipment.TerminalToTerminalConnections != null)
                newTerminalConnectionsList.AddRange(existingEquipment.TerminalToTerminalConnections);

            newTerminalConnectionsList.Add(
                new TerminalToTerminalConnection(
                    fromTerminalId: @event.FromTerminalId,
                    toTerminalId: @event.ToTerminalId
                 )
             );

            return existingEquipment with
            {
                TerminalToTerminalConnections = newTerminalConnectionsList.ToArray()
            };
        }


        public static NodeContainer Apply(NodeContainer existingEquipment, NodeContainerTerminalsDisconnected @event)
        {
            List<TerminalToTerminalConnection> newTerminalConnectionsList = new();

            if (existingEquipment.TerminalToTerminalConnections != null)
            {
                foreach (var existingConnection in existingEquipment.TerminalToTerminalConnections)
                {
                    if ((existingConnection.FromTerminalId == @event.FromTerminalId && existingConnection.ToTerminalId == @event.ToTerminalId) || (existingConnection.FromTerminalId == @event.ToTerminalId && existingConnection.ToTerminalId == @event.FromTerminalId))
                    {
                        continue;
                    }

                    newTerminalConnectionsList.Add(existingConnection);
                }
            }

            return existingEquipment with
            {
                TerminalToTerminalConnections = newTerminalConnectionsList.ToArray()
            };
        }


    }
}
