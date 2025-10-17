using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.Schematic.Business.SchematicBuilder
{
    /// <summary>
    /// View model serving diagram creation of a node container
    /// </summary>
    public class NodeContainerViewModel
    {
        private readonly Guid _elementNodeId;
        private readonly RouteNetworkElementRelatedData _data;

        public RouteNetworkElementRelatedData Data => _data;
        public NodeContainer NodeContainer { get; }
        public bool HasRacksOrTerminalEquipments
        {
            get {
                if (Data.NodeContainer.Racks != null && Data.NodeContainer.Racks.Length > 0)
                    return true;

                if (Data.NodeContainer.TerminalEquipmentReferences != null && Data.NodeContainer.TerminalEquipmentReferences.Length > 0)
                    return true;

                return false;
            }
        }

        public NodeContainerViewModel(RouteNetworkElementRelatedData data)
        {
            _data = data;
            
            if (_data.NodeContainer == null)
                throw new ApplicationException("This view model requires a RouteNetworkElementRelatedData object with a non-null NodeContainer object!");

            NodeContainer = _data.NodeContainer;
        }

        public string GetNodeContainerTypeLabel()
        {
            return _data.NodeContainerSpecifications[_data.NodeContainer.SpecificationId].Name;
        }

        public List<RackViewModel> GetRackViewModels()
        {
            List<RackViewModel> rackViewModels = new();

            if (Data.NodeContainer.Racks != null)
            {
                foreach (var rack in Data.NodeContainer.Racks)
                {
                    var rackSpec = Data.RackSpecifications[rack.SpecificationId];

                    rackViewModels.Add(new RackViewModel()
                    {
                        RackId = rack.Id,
                        Name = rack.Name,
                        SpecName = rackSpec.ShortName,
                        MinHeightInUnits = rack.HeightInUnits,
                        TerminalEquipments = GetTerminalEquipmentViewModelsForRack(rack.Id)
                    });
                }
            }

            return rackViewModels;
        }

        public List<TerminalEquipmentViewModel> GetStandaloneTerminalEquipmentViewModels()
        {
            List<TerminalEquipmentViewModel> viewModels = new();

            if (Data.NodeContainer.TerminalEquipmentReferences != null)
            {
                foreach (var terminalEquipmentRef in Data.NodeContainer.TerminalEquipmentReferences)
                {
                    var terminalEquipment = Data.TerminalEquipments[terminalEquipmentRef];
                    var terminalEquipmentSpecification =Data.TerminalEquipmentSpecifications[terminalEquipment.SpecificationId];

                    viewModels.Add(new TerminalEquipmentViewModel()
                    {
                        TerminalEquipmentId = terminalEquipment.Id,
                        Name = terminalEquipment.Name,
                        SpecName = terminalEquipmentSpecification.ShortName,
                        Style = terminalEquipmentSpecification.IsCustomerTermination ? "TerminalEquipmentWithProperties" : "TerminalEquipment"
                    });
                }
            }

            return viewModels;
        }

        public List<TerminalEquipmentViewModel> GetTerminalEquipmentViewModelsForRack(Guid rackId)
        {
            List<TerminalEquipmentViewModel> viewModels = new();

            if (Data.NodeContainer.Racks != null)
            {
                var rack = Data.NodeContainer.Racks.First(r => r.Id == rackId);

                foreach (var subrack in rack.SubrackMounts)
                {
                    var terminalEquipment = Data.TerminalEquipments[subrack.TerminalEquipmentId];
                    var terminalEquipmentSpecification = Data.TerminalEquipmentSpecifications[terminalEquipment.SpecificationId];

                    viewModels.Add(new TerminalEquipmentViewModel()
                    {
                        TerminalEquipmentId = terminalEquipment.Id,
                        SubrackPosition = subrack.Position,
                        SubrackHeight = subrack.HeightInUnits,
                        Name = terminalEquipment.Name,
                        SpecName = terminalEquipmentSpecification.ShortName,
                        Style = terminalEquipmentSpecification.IsCustomerTermination ? "TerminalEquipmentWithProperties" : "TerminalEquipment"
                    });
                }
            }
          
            return viewModels;
        }

        public string GetLabelForEquipmentConnectedToCable(Guid cableId)
        {
            var foundTerminalEquipment = FindEquipmentConnectedToCableSegment(cableId);

            if (foundTerminalEquipment != null)
            {
                // If more than one rack, show rack name as well
                if (Data.NodeContainer.Racks != null && Data.NodeContainer.Racks.Length > 1)
                {
                    var rack = FindRackContainingEquipment(foundTerminalEquipment.Id);

                    if (rack != null && rack.Name != null)
                        return rack.Name + " " + foundTerminalEquipment.Name;
                }

                return foundTerminalEquipment.Name;
            }


            return null;
        }

        private Rack FindRackContainingEquipment(Guid terminalEquipmentId)
        {
            if (Data.NodeContainer.Racks != null)
            {
                foreach (var rack in Data.NodeContainer.Racks)
                {
                    foreach (var subRack in rack.SubrackMounts)
                    {
                        if (subRack.TerminalEquipmentId == terminalEquipmentId)
                            return rack;
                    }
                }
            }

            return null;
        }

        private TerminalEquipment FindEquipmentConnectedToCableSegment(Guid cableId)
        {
            // Create hash with cable terminal ids for quick lookup
            var cable = Data.SpanEquipments[cableId];

            HashSet<Guid> cableTerminalIds = new HashSet<Guid>();

            foreach (var cableStructure in cable.SpanStructures)
            {
                if (cableStructure.SpanSegments.Length > 0)
                {
                    if (cableStructure.SpanSegments[0].FromTerminalId != Guid.Empty)
                        cableTerminalIds.Add(cableStructure.SpanSegments[0].FromTerminalId);

                    if (cableStructure.SpanSegments[0].ToTerminalId != Guid.Empty)
                        cableTerminalIds.Add(cableStructure.SpanSegments[0].ToTerminalId);
                }
            }

            TerminalEquipment foundTerminalEquipment = null;

            if (NodeContainer.TerminalEquipmentReferences != null)
            {
                foreach (var terminalEquipmentId in NodeContainer.TerminalEquipmentReferences)
                {
                    var terminalEquipment = Data.TerminalEquipments[terminalEquipmentId];

                    foreach (var terminalStructure in terminalEquipment.TerminalStructures)
                    {
                        foreach (var terminal in terminalStructure.Terminals)
                        {
                            if (cableTerminalIds.Contains(terminal.Id))
                            {
                                foundTerminalEquipment = terminalEquipment;
                                break;
                            }
                        }
                    }
                }
            }

            if (NodeContainer.Racks != null)
            {
                foreach (var rack in NodeContainer.Racks)
                {
                    foreach (var subRack in rack.SubrackMounts)
                    {
                        var terminalEquipment = Data.TerminalEquipments[subRack.TerminalEquipmentId];

                        foreach (var terminalStructure in terminalEquipment.TerminalStructures)
                        {
                            foreach (var terminal in terminalStructure.Terminals)
                            {
                                if (cableTerminalIds.Contains(terminal.Id))
                                {
                                    foundTerminalEquipment = terminalEquipment;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            return foundTerminalEquipment;
        }


        public List<NodeContainerBlockPortViewModel> PortViewModels = new();
    }
 }
