using FluentResults;
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

namespace OpenFTTH.UtilityGraphService.Business.Trace.Util
{
    public class RelatedDataHolder
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly UtilityNetworkProjection _utilityNetwork;
        private LookupCollection<TerminalEquipmentSpecification> _terminalEquipmentSpecifications;
        private LookupCollection<TerminalStructureSpecification> _terminalStructureSpecifications;

        public Dictionary<Guid, RouteNetworkElement> RouteNetworkElementById { get; }
        public Dictionary<Guid, NodeContainer> NodeContainerById  { get; }
        public Dictionary<Guid, string> AddressStringById { get; }
        public Dictionary<Guid, RouteNetworkInterest> RouteNetworkInterestById { get; }

        public RelatedDataHolder(IEventStore eventStore, UtilityNetworkProjection utilityNetwork, IQueryDispatcher queryDispatcher, IEnumerable<Guid> nodeOfInterestIds, HashSet<Guid>? addressIds = null, IEnumerable<Guid>? segmentWalkOfInterestIds = null)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _utilityNetwork = utilityNetwork;

            _terminalEquipmentSpecifications = _eventStore.Projections.Get<TerminalEquipmentSpecificationsProjection>().Specifications;
            _terminalStructureSpecifications = _eventStore.Projections.Get<TerminalStructureSpecificationsProjection>().Specifications;

            RouteNetworkElementById = GatherRouteNetworkElementInformation(nodeOfInterestIds);

            NodeContainerById = GatcherNodeContainerInformation(RouteNetworkElementById.Values.ToList());

            AddressStringById = GatherAddressInformation(addressIds);

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

        private Dictionary<Guid, string> GatherAddressInformation(HashSet<Guid>? addressIdsToQuery)
        {
            if (addressIdsToQuery == null)
                return new Dictionary<Guid, string>();

            var getAddressInfoQuery = new GetAddressInfo(addressIdsToQuery.ToArray());

            var addressResult = _queryDispatcher.HandleAsync<GetAddressInfo, Result<GetAddressInfoResult>>(getAddressInfoQuery).Result;

            Dictionary<Guid, string> result = new();

            if (addressResult.IsSuccess)
            {
                foreach (var addressHit in addressResult.Value.AddressHits)
                {
                    if (addressHit.RefClass == AddressEntityClass.UnitAddress)
                    {
                        var unitAddress = addressResult.Value.UnitAddresses[addressHit.RefId];
                        var accessAddress = addressResult.Value.AccessAddresses[unitAddress.AccessAddressId];

                        var addressStr = accessAddress.RoadName + " " + accessAddress.HouseNumber;

                        if (unitAddress.FloorName != null)
                            addressStr += (", " + unitAddress.FloorName);

                        if (unitAddress.SuitName != null)
                            addressStr += (" " + unitAddress.SuitName);

                        result.Add(addressHit.Key, addressStr);
                    }
                    else
                    {
                        var accessAddress = addressResult.Value.AccessAddresses[addressHit.RefId];

                        var addressStr = accessAddress.RoadName + " " + accessAddress.HouseNumber;

                        result.Add(addressHit.Key, addressStr);
                    }
                }
            }
            else
            {
                throw new ApplicationException($"Error calling address service from trace. Error: " + addressResult.Errors.First().Message);
            }

            return result;
        }

        public string? GetAddressString(Guid? addressId)
        {
            if (addressId == null || addressId.Value == Guid.Empty)
                return null;

            if (AddressStringById.ContainsKey(addressId.Value))
                return AddressStringById[addressId.Value];
            else
                return null;
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
            string? addressInfo = null;

            if (terminalEquipment.AddressInfo != null)
            {
                addressInfo = GetAddressString(GetTerminalEquipmentMostAccurateAddressId(terminalEquipment));

                if (addressInfo != null)
                    addressInfo = " (" + addressInfo + ")";
            }

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
                addressInfo = GetAddressString(GetTerminalEquipmentMostAccurateAddressId(terminalEquipment));

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
            if (terminalEquipment.TerminalStructures.Length == 1)
            {
                var terminal = terminalEquipment.TerminalStructures[0].Terminals[0];
                var terminalEquipmentSpecification = _terminalEquipmentSpecifications[terminalEquipment.SpecificationId];

                // If rack equipment where type is not put into name
                if (IsTerminalEquipmentRackTray(terminalEquipment, terminalEquipmentSpecification))
                {
                    if (terminal.IsSplice)
                        return "Bakke " + terminalEquipment.Name;
                    else
                        return "Kort " + terminalEquipment.Name;
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
                    string slotType = terminalStructureSpec.Category.ToLower().Contains("splice") ? "Bakke" : "Kort";

                    return $"{slotType} {terminalStructure.Position}";
                }
                else
                {
                    return GetInterfaceName(terminalStructure);
                }
            }
        }

        public string GetCircuitName(IUtilityGraphTerminalRef terminalRef)
        {
            var terminalStructure = terminalRef.TerminalStructure(_utilityNetwork);

            if (terminalStructure.interfaceInfo != null && terminalStructure.interfaceInfo.CircuitName != null)
                return terminalStructure.interfaceInfo.CircuitName;

            return null;
        }

        public bool IsCustomerSplitter(IUtilityGraphTerminalRef terminalRef)
        {
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
                string pinType = terminalStructureSpec.Category.ToLower().Contains("splice") ? "Søm" : "Port";

                return $"{pinType} {terminal.Name}";
            }
        }

        public string GetSpanEquipmentFullFiberCableString(SpanEquipment spanEquipment, int fiberNo)
        {
            int fiber = ((fiberNo - 1) % 12) + 1;
            int tube = ((fiberNo - 1) / 12) + 1;

            return $"{spanEquipment.Name} ({spanEquipment.SpanStructures.Length - 1}) Tube {tube} Fiber {fiber}";
        }

        public string GetSpanEquipmentTubeFiberString(SpanEquipment spanEquipment, int fiberNo)
        {
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
    }
}
