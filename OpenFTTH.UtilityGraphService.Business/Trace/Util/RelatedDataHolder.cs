using OpenFTTH.Results;
using OpenFTTH.Address.API.Model;
using OpenFTTH.Address.API.Queries;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Projections;
using OpenFTTH.UtilityGraphService.Business.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using OpenFTTH.UtilityGraphService.Business.Graph.Projections;

namespace OpenFTTH.UtilityGraphService.Business.Trace.Util
{
    public class RelatedDataHolder
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly UtilityNetworkProjection _utilityNetwork;
        private readonly AddressInfoProjection _addressInfo;
        private readonly InstallationProjection _installationInfo;

        private LookupCollection<TerminalEquipmentSpecification> _terminalEquipmentSpecifications;
        private LookupCollection<TerminalStructureSpecification> _terminalStructureSpecifications;
        private LookupCollection<SpanEquipmentSpecification> _spanEquipmentSpecifications;
        private LookupCollection<SpanStructureSpecification> _spanStructureSpecifications;

        public Dictionary<Guid, RouteNetworkElement> RouteNetworkElementById { get; }
        public Dictionary<Guid, NodeContainer> NodeContainerById  { get; }
        public Dictionary<Guid, RouteNetworkInterest> RouteNetworkInterestById { get; }

        public RelatedDataHolder(IEventStore eventStore, UtilityNetworkProjection utilityNetwork, IQueryDispatcher queryDispatcher, IEnumerable<Guid> nodeOfInterestIds, IEnumerable<Guid>? segmentWalkOfInterestIds = null)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _utilityNetwork = utilityNetwork;

            _terminalEquipmentSpecifications = _eventStore.Projections.Get<TerminalEquipmentSpecificationsProjection>().Specifications;
            _terminalStructureSpecifications = _eventStore.Projections.Get<TerminalStructureSpecificationsProjection>().Specifications;
            _spanEquipmentSpecifications = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;
            _spanStructureSpecifications = _eventStore.Projections.Get<SpanStructureSpecificationsProjection>().Specifications;
            _addressInfo = eventStore.Projections.Get<AddressInfoProjection>();
            _installationInfo = eventStore.Projections.Get<InstallationProjection>();


            RouteNetworkElementById = GatherRouteNetworkElementInformation(nodeOfInterestIds);

            NodeContainerById = GatcherNodeContainerInformation(RouteNetworkElementById.Values.ToList());

            // Now fetch span segment interests and related route network elements
            if (segmentWalkOfInterestIds != null)
            {
                var segmentWoiIds = new InterestIdList();
                segmentWoiIds.AddRange(segmentWalkOfInterestIds);

                QueryHelper.GetInterestsAndRouteNetworkElementsByInterestIds(_queryDispatcher, segmentWoiIds, out var routeNetworkInterests, out var routeNetworkElements);
                RouteNetworkInterestById = routeNetworkInterests.ToDictionary(x => x.Id);

                foreach (var routeNetworkElement in routeNetworkElements)
                {
                    if (!RouteNetworkElementById.ContainsKey(routeNetworkElement.Id))
                    {
                        RouteNetworkElementById.Add(routeNetworkElement.Id, routeNetworkElement);
                    }
                }
            }
        }

        private Dictionary<Guid, RouteNetworkElement> GatherRouteNetworkElementInformation(IEnumerable<Guid> nodeOfInterestIds)
        {
            if (nodeOfInterestIds.Count() == 0)
            {
                return new Dictionary<Guid, RouteNetworkElement>();
            }

            RouteNetworkElementIdList idList = new();
            idList.AddRange(nodeOfInterestIds);

            var routeNetworkQueryResult = _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
                new GetRouteNetworkDetails(idList)
                {
                    RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementOnly,
                    RouteNetworkElementFilter = new RouteNetworkElementFilterOptions()
                    {
                        IncludeNamingInfo = true,
                        IncludeRouteNodeInfo = true
                    }
                }
            ).Result;

            if (routeNetworkQueryResult.IsFailed)
                throw new ApplicationException("Failed to query route network information. Got error: " + routeNetworkQueryResult.Errors.First().Message);

            return routeNetworkQueryResult.Value.RouteNetworkElements.ToDictionary(x => x.Id);
        }
  
        private Dictionary<Guid, NodeContainer> GatcherNodeContainerInformation(List<RouteNetworkElement> routeNetworkElements)
        {
            Dictionary<Guid, NodeContainer> result = new();

            // Get node containers
            foreach (var routeNetworkElement in RouteNetworkElementById.Values)
            {
                if (routeNetworkElement.InterestRelations != null)
                {
                    foreach (var interestRel in routeNetworkElement.InterestRelations)
                    {
                        if (_utilityNetwork.TryGetEquipment<NodeContainer>(interestRel.RefId, out var nodeContainer))
                        {
                            result.Add(routeNetworkElement.Id, nodeContainer);
                        }
                    }
                }
            }

            return result;
        }

        public string? GetNodeName(Guid routeNodeId)
        {
            if (RouteNetworkElementById != null && RouteNetworkElementById.ContainsKey(routeNodeId))
            {
                var node = RouteNetworkElementById[routeNodeId];
                return node.Name;
            }

            return null;
        }

        public string? GetNodeOrEquipmentName(Guid routeNodeId, TerminalEquipment terminalEquipment)
        {
            if (RouteNetworkElementById != null && RouteNetworkElementById.ContainsKey(routeNodeId))
            {
                var nodeName = RouteNetworkElementById[routeNodeId].Name;

                if (!String.IsNullOrEmpty(nodeName))
                    return nodeName;
            }

            return terminalEquipment.Name;
        }

        public string? GetRackName(Guid routeNodeId, Guid terminalEquipmentId)
        {
            if (NodeContainerById.ContainsKey(routeNodeId))
            {
                var container = NodeContainerById[routeNodeId];

                if (container.Racks != null)
                {
                    foreach (var rack in container.Racks)
                    {
                        if (rack.SubrackMounts != null && rack.SubrackMounts.Any(m => m.TerminalEquipmentId == terminalEquipmentId))
                            return rack.Name;
                    }
                }
            }

            return null;
        }

        public string? GetNodeAndEquipmentEndString(UtilityGraphConnectedTerminal terminalRef)
        {
            var nodeName = GetNodeName(terminalRef.RouteNodeId);

            if (terminalRef.IsDummyEnd)
                return $"{nodeName} løs ende";

            var terminalEquipment = terminalRef.TerminalEquipment(_utilityNetwork);
       
            // Prepare node name if available
            if (nodeName != null)
                nodeName += " ";

            // Prepare address info if available
            string? addressInfo = GetTerminalEquipmentAddressLabel(terminalEquipment);

            var equipmentName = GetEquipmentWithStructureInfoString(terminalRef);

            return $"{nodeName}{equipmentName}{addressInfo}";
        }

        public string GetFullEquipmentString(Guid routeNodeId, TerminalEquipment terminalEquipment, bool includeNodeName = false, bool includeAddressInfo = false)
        {
            var rackName = GetRackName(routeNodeId, terminalEquipment.Id);

            string? nodeName = null;

            if (includeNodeName)
            {
                nodeName = GetNodeName(routeNodeId);

                if (nodeName != null)
                    nodeName += " - ";
            }

            string? addressInfo = null;

            if (includeAddressInfo && terminalEquipment.AddressInfo != null && terminalEquipment.AddressInfo.AccessAddressId != null)
            {
                addressInfo = GetTerminalEquipmentAddressLabel(terminalEquipment);
                    
                if (addressInfo != null)
                    addressInfo = " (" + addressInfo + ")";
            }

            if (rackName != null)
                return $"{nodeName}{rackName} - {GetEquipmentName(terminalEquipment)}{addressInfo}";
            else
                return $"{nodeName}{GetEquipmentName(terminalEquipment)}{addressInfo}";
        }

        public string GetCompactEquipmentWithTypeInfoString(Guid routeNodeId, TerminalEquipment terminalEquipment, bool includeNodeName = false)
        {
            var rackName = GetRackName(routeNodeId, terminalEquipment.Id);

            string? nodeName = null;

            if (includeNodeName)
            {
                nodeName = GetNodeName(routeNodeId);

                if (nodeName != null)
                    nodeName += " - ";
            }

            var terminalEquipmentSpecification = _terminalEquipmentSpecifications[terminalEquipment.SpecificationId];


            if (rackName != null)
            {
                if (IsTerminalEquipmentRackTray(terminalEquipment, terminalEquipmentSpecification))
                {
                    return $"{nodeName}{rackName} ({terminalEquipmentSpecification.ShortName})";
                }
                else
                {
                    return $"{nodeName}{rackName} - {GetEquipmentName(terminalEquipment)} ({terminalEquipmentSpecification.ShortName})";
                }
            }
            else
            {
                if (IsCustomerTermination(terminalEquipment, terminalEquipmentSpecification))
                {
                    return $"{terminalEquipmentSpecification.ShortName}";
                }
                else
                {
                    return $"{nodeName}{GetEquipmentName(terminalEquipment)} ({terminalEquipmentSpecification.ShortName})";
                }
            }
        }

        public string? GetEquipmentName(TerminalEquipment terminalEquipment)
        {
            // Single structure equipment
            if (terminalEquipment.TerminalStructures.Length == 1 && terminalEquipment.TerminalStructures[0].Terminals.Length > 0)
            {
                var terminal = terminalEquipment.TerminalStructures[0].Terminals[0];
                var terminalEquipmentSpecification = _terminalEquipmentSpecifications[terminalEquipment.SpecificationId];
                var terminalStructureSpecification = _terminalStructureSpecifications[terminalEquipment.TerminalStructures[0].SpecificationId];

                // If rack equipment where type is not put into name
                if (IsTerminalEquipmentRackTray(terminalEquipment, terminalEquipmentSpecification))
                {
                    string slotType = null;

                    if (terminal.IsSplice)
                        slotType = "Bakke";
                    else
                        slotType = "Kort";

                    if (!String.IsNullOrEmpty(terminalStructureSpecification.SlotTypeAlias))
                        slotType = terminalStructureSpecification.SlotTypeAlias;

                    return slotType + " " + terminalEquipment.Name;
                }
            }

            return terminalEquipment.Name;
        }

        public string GetEquipmentWithStructureInfoString(IUtilityGraphTerminalRef terminalRef)
        {
            if (terminalRef.IsDummyEnd)
                return $"løs ende";

            var terminalEquipment = terminalRef.TerminalEquipment(_utilityNetwork);

            var terminalStructure = terminalRef.TerminalStructure(_utilityNetwork);

            var terminal = terminalRef.Terminal(_utilityNetwork);

            var rackName = GetRackName(terminalRef.RouteNodeId, terminalEquipment.Id);

            if (terminalStructure.interfaceInfo == null)
            {
                string? terminalStructurePosition = null;

                // Show card position on all patch equipment and splice equipment with more than one card
                if (!terminal.IsSplice || (terminalEquipment.TerminalStructures.Length > 1))
                {
                    terminalStructurePosition = $"-{terminalStructure.Position}";
                }

                if (rackName != null)
                    return $"{rackName}-{terminalEquipment.Name}{terminalStructurePosition}-{terminal.Name}";
                else
                    return $"{terminalEquipment.Name}{terminalStructurePosition}-{terminal.Name}";
            }
            else
            {
                string interfaceName = GetInterfaceName(terminalStructure);

                if (rackName != null)
                    return $"{rackName}-{terminalEquipment.Name}-{terminal.Name}-{interfaceName}";
                else
                    return $"{terminalEquipment.Name}-{terminal.Name}-{interfaceName}";

            }
        }

        public static string GetInterfaceName(TerminalStructure terminalStructure)
        {
            string interfaceName = terminalStructure.interfaceInfo.InterfaceType + "-" + terminalStructure.interfaceInfo.SlotNumber;

            interfaceName += ("/" + terminalStructure.interfaceInfo.SubSlotNumber);

            interfaceName += ("/" + terminalStructure.interfaceInfo.PortNumber);

            if (terminalStructure.interfaceInfo.CircuitName != null)
                interfaceName += (" (" + terminalStructure.interfaceInfo.CircuitName + ")");

            return interfaceName;
        }

        public string GetEquipmentStructureInfoString(IUtilityGraphTerminalRef terminalRef)
        {
            if (terminalRef.IsDummyEnd)
                return $"løs ende";

            var terminalEquipment = terminalRef.TerminalEquipment(_utilityNetwork);

            var terminalEquipmentSpecification = _terminalEquipmentSpecifications[terminalEquipment.SpecificationId];

            var terminalStructure = terminalRef.TerminalStructure(_utilityNetwork);

            var terminalStructureSpec = _terminalStructureSpecifications[terminalStructure.SpecificationId];

            // If rack equipment where type is not put into name
            if (IsTerminalEquipmentRackTray(terminalEquipment, terminalEquipmentSpecification))
            {
                return GetEquipmentName(terminalEquipment);
            }
            // Subrack shelf to hold splitter modules
            else if (terminalStructureSpec.Category == "Splitters" && terminalEquipmentSpecification.StructureTemplates.Count() == 0)
            {
                return $"Splitter {terminalStructure.Position}";
            }
            else
            {
                if (terminalStructure.interfaceInfo == null)
                {
                    if (terminalStructureSpec.IsPassThrough)
                        return "Gennemsplidsning";

                    string slotType = terminalStructureSpec.Category.ToLower().Contains("splice") ? "Bakke" : "Kort";

                    if (terminalStructureSpec.SlotTypeAlias != null)
                        slotType = terminalStructureSpec.SlotTypeAlias;

                    string posName = terminalStructure.Position.ToString();

                    if (terminalEquipmentSpecification.TerminalStructuresIsNameable)
                        posName = terminalStructure.Name;

                    return $"{slotType} {posName}";
                }
                else
                {
                    return GetInterfaceName(terminalStructure);
                }
            }
        }

        public string GetCircuitName(IUtilityGraphTerminalRef terminalRef)
        {
            if (terminalRef.IsDummyEnd)
                return null;

            var terminalStructure = terminalRef.TerminalStructure(_utilityNetwork);

            if (terminalStructure.interfaceInfo != null && terminalStructure.interfaceInfo.CircuitName != null)
                return terminalStructure.interfaceInfo.CircuitName;

            return null;
        }

        public bool IsCustomerSplitter(IUtilityGraphTerminalRef terminalRef)
        {
            if (terminalRef.IsDummyEnd)
                return false;

            // Check if equipment is marked as customer spitter
            var terminalEquipment = terminalRef.TerminalEquipment(_utilityNetwork);

            var terminalEquipmentSpecification = _terminalEquipmentSpecifications[terminalEquipment.SpecificationId];

            if (terminalEquipmentSpecification.IsCustomerSplitter)
                return true;


            // Check if structure is marked as customer splitter
            var terminalStructure = terminalRef.TerminalStructure(_utilityNetwork);

            var terminalStructureSpec = _terminalStructureSpecifications[terminalStructure.SpecificationId];

            if (terminalStructureSpec.IsCustomerSplitter)
                return true;

            return false;
        }

        public bool IsLineTermination(IUtilityGraphTerminalRef terminalRef)
        {
            if (terminalRef.IsDummyEnd)
                return false;

            var terminalEquipment = terminalRef.TerminalEquipment(_utilityNetwork);

            var terminalEquipmentSpecification = _terminalEquipmentSpecifications[terminalEquipment.SpecificationId];

            return terminalEquipmentSpecification.IsLineTermination;
        }


        public string GetEquipmentTerminalInfoString(IUtilityGraphTerminalRef terminalRef)
        {
            if (terminalRef.IsDummyEnd)
                return $"løs ende";

            var terminalStructure = terminalRef.TerminalStructure(_utilityNetwork);

            var terminal = terminalRef.Terminal(_utilityNetwork);

            var terminalStructureSpec = _terminalStructureSpecifications[terminalStructure.SpecificationId];


            if (terminalStructureSpec.Category == "Splitters")
            {
                return terminal.Name;
            }
            else
            {
                if (terminalStructureSpec.IsPassThrough)
                    return "";

                string pinType = terminalStructureSpec.Category.ToLower().Contains("splice") ? "Søm" : "Port";

                if (!String.IsNullOrEmpty(terminalStructureSpec.PortTypeAlias))
                    pinType = terminalStructureSpec.PortTypeAlias;

                return $"{pinType} {terminal.Name}";
            }
        }

        public string GetSpanEquipmentFullFiberCableString(SpanEquipment spanEquipment, int fiberNo)
        {
            var spanEquipmentSpecification = _spanEquipmentSpecifications[spanEquipment.SpecificationId];

            var formattedText = spanEquipmentSpecification.GetFormattedCableString(spanEquipment.Name, fiberNo, true, _spanStructureSpecifications);

            if (formattedText != null)
                return formattedText;

            int fiber = ((fiberNo - 1) % 12) + 1;
            int tube = ((fiberNo - 1) / 12) + 1;

            return $"{spanEquipment.Name} ({spanEquipment.SpanStructures.Length - 1}) Tube {tube} Fiber {fiber}";
        }

        public string GetSpanEquipmentTubeFiberString(SpanEquipment spanEquipment, int fiberNo)
        {
            var spanEquipmentSpecification = _spanEquipmentSpecifications[spanEquipment.SpecificationId];

            var formattedText = spanEquipmentSpecification.GetFormattedCableString(spanEquipment.Name, fiberNo, false, _spanStructureSpecifications);

            if (formattedText != null)
                return formattedText;

            int fiber = ((fiberNo - 1) % 12) + 1;
            int tube = ((fiberNo - 1) / 12) + 1;

            return $"Tube {tube} Fiber {fiber}";
        }

        public string GetSpanStructureConduitString(SpanEquipment spanEquipment, int conduitNo, SpanStructureSpecification spanStructureSpecification)
        {
            return spanStructureSpecification.Name.Replace("_", " ");
        }

        private Guid? GetTerminalEquipmentMostAccurateAddressId(TerminalEquipment terminalEquipment)
        {
            if (terminalEquipment.AddressInfo != null && terminalEquipment.AddressInfo.UnitAddressId != null)
                return terminalEquipment.AddressInfo.UnitAddressId.Value;
            else if (terminalEquipment.AddressInfo != null && terminalEquipment.AddressInfo.AccessAddressId != null)
                return terminalEquipment.AddressInfo.AccessAddressId.Value;

            return null;
        }

        private bool IsTerminalEquipmentRackTray(TerminalEquipment terminalEquipment, TerminalEquipmentSpecification terminalEquipmentSpecification)
        {
            // This is a hack to support LISA equipment modelled with only rack and tray level. Normally there will be rack, subrack, and tray level

            // If equipment name is numeric, treat the subrack as a tray
            if (terminalEquipmentSpecification.IsRackEquipment && int.TryParse(terminalEquipment.Name, out _))
                return true;
            // If the first word in equipment name is numeric, treat the subrack as a tray
            else if (terminalEquipmentSpecification.IsRackEquipment && terminalEquipment.Name != null && terminalEquipment.Name.Split(" ").Count() > 1 && int.TryParse(terminalEquipment.Name.Split(" ")[0], out _))
                return true;
            else
                return false;
        }

        private bool IsCustomerTermination(TerminalEquipment terminalEquipment, TerminalEquipmentSpecification terminalEquipmentSpecification)
        {
            // TODO: Remove this after conversion fix
            if (terminalEquipmentSpecification.Category != null && (terminalEquipmentSpecification.Category == "Kundeterminering"))
                return true;

            if (terminalEquipmentSpecification.IsCustomerTermination)
                return true;
            else
                return false;
        }

        public string GetTerminalEquipmentAddressLabel(TerminalEquipment terminalEquipment)
        {
            string? addressLabel = null;

            var terminalEquipmentSpecification = _terminalEquipmentSpecifications[terminalEquipment.SpecificationId];

            // In case of a customer termination, try find address info through installation information retrieved from external system
            if (terminalEquipmentSpecification.IsCustomerTermination)
            {
                var instInfo = _installationInfo.GetInstallationInfo(terminalEquipment.Name, _utilityNetwork);

                if (instInfo != null && instInfo.UnitAddressId != null && instInfo.UnitAddressId != Guid.Empty)
                {
                    addressLabel = GetAddressLabel((Guid)instInfo.UnitAddressId);
                }
            }

            // Try find address info related to terminal equipment
            if (addressLabel == null && terminalEquipment.AddressInfo != null)
            {
                addressLabel = GetAddressLabel(GetTerminalEquipmentMostAccurateAddressId(terminalEquipment));

                if (addressLabel != null)
                    addressLabel = " (" + addressLabel + ")";
            }

            return addressLabel;
        }
        
        public string? GetAddressLabel(Guid? accessOrUnitAddressId)
        {
            if (accessOrUnitAddressId == null)
                return null;

            if (_addressInfo.UnitAddressesById.TryGetValue((Guid)accessOrUnitAddressId, out var unitAddress))
            {
                if (_addressInfo.AccessAddressesById.TryGetValue(unitAddress.AccessAddressId, out var accessAddress))
                {
                    if (accessAddress.RoadId != null && _addressInfo.RoadsById.TryGetValue((Guid)accessAddress.RoadId, out var road))
                    {
                        return (road.Name + " " + accessAddress.HouseNumber + " " + unitAddress.FloorName + " " + unitAddress.SuitName).Trim();
                    }
                }
            }

            if (_addressInfo.AccessAddressesById.TryGetValue((Guid)accessOrUnitAddressId, out var accessAddressOnly))
            {
                if (accessAddressOnly.RoadId != null && _addressInfo.RoadsById.TryGetValue((Guid)accessAddressOnly.RoadId, out var road))
                {
                    return (road.Name + " " + accessAddressOnly.HouseNumber).Trim();
                }
            }

            return null;
        }

    }
}
