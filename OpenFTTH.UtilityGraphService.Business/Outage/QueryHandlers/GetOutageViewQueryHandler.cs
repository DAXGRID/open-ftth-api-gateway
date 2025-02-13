using Baseline;
using Baseline.ImTools;
using FluentResults;
using Marten;
using OpenFTTH.Address.API.Model;
using OpenFTTH.Address.API.Queries;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.Outage;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Projections;
using OpenFTTH.UtilityGraphService.Business.Trace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.Outage.QueryHandlers
{
    public class GetOutageViewQueryHandler : IQueryHandler<GetOutageView, Result<OutageViewNode>>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly UtilityNetworkProjection _utilityNetwork;
        private LookupCollection<SpanEquipmentSpecification> _spanEquipmentSpecifications;
        private LookupCollection<SpanStructureSpecification> _spanStructureSpecifications;
        private LookupCollection<TerminalEquipmentSpecification> _terminalEquipmentSpecifications;
        private LookupCollection<TerminalStructureSpecification> _terminalStructureSpecifications;


        public GetOutageViewQueryHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
        }

        public Task<Result<OutageViewNode>> HandleAsync(GetOutageView query)
        {
     
            var networkInterestsResult = GetRouteNetworkElementEquipmentOfInterest(query.RouteNetworkElementId);

            if (networkInterestsResult.IsFailed)
                return Task.FromResult(Result.Fail<OutageViewNode>(networkInterestsResult.Errors.First()));

            if (networkInterestsResult.Value.IsNode)
            {
                return Task.FromResult(Result.Ok(GetOutageViewForRouteNode(networkInterestsResult.Value, query.EquipmentId)));
            }
            else
            {
                return Task.FromResult(Result.Ok(GetOutageViewForRouteSegment(networkInterestsResult.Value)));
            }
        }

        private OutageViewNode GetOutageViewForRouteNode(OutageProcessingState processingState, Guid? equipmentId)
        {
            if (equipmentId == null)
                throw new ApplicationException($"EquipmentId is missing. Must be provided when doing outage queries inside route nodes");

            if (processingState.NodeContainer == null)
                throw new ApplicationException($"No node container found in route node with id: {processingState.RouteElementId} Must be present when doing outage queries inside route nodes");

            if (!processingState.AnyEquipment(equipmentId.Value))
                throw new ApplicationException($"Can't find any terminal equipment or rack with id: {equipmentId.Value} in node: {processingState.RouteElementId}");

            if (processingState.TerminalEquipments == null)
                throw new ApplicationException($"No terminal equipments found in route node with id: {processingState.RouteElementId}");

            OutageViewNode rootNode = new OutageViewNode(Guid.NewGuid(), "{OutageViewRouteNode}");

            _terminalEquipmentSpecifications = _eventStore.Projections.Get<TerminalEquipmentSpecificationsProjection>().Specifications;
            _terminalStructureSpecifications = _eventStore.Projections.Get<TerminalStructureSpecificationsProjection>().Specifications;

            if (processingState.NodeContainer == null)
            {
                rootNode.Description = "{OutageViewNoRelatedEquipmentsInRouteSegment}";
                return rootNode;
            }

            if (processingState.IsTerminalEquipment(equipmentId.Value))
            {
                AddTerminalEquipmentToOutageList(processingState, equipmentId, rootNode);
            }
            else if (processingState.IsRackEquipment(equipmentId.Value))
            {
                foreach (var subRack in processingState.NodeContainer.Racks.FindFirst(r => r.Id == equipmentId.Value).SubrackMounts.OrderBy(s => s.Position).Reverse())
                {
                    AddTerminalEquipmentToOutageList(processingState, subRack.TerminalEquipmentId, rootNode);
                }
            }

            AddAddressInformationToInstallations(processingState);

            return rootNode;
        }

        private void AddTerminalEquipmentToOutageList(OutageProcessingState processingState, Guid? equipmentId, OutageViewNode rootNode)
        {
            var terminalEquipment = processingState.TerminalEquipments[equipmentId.Value];

            var eqSpecification = _terminalEquipmentSpecifications[terminalEquipment.SpecificationId];

            var terminalEquipmentNode = new OutageViewNode(Guid.NewGuid(), GetTerminalEquipmentLabel(terminalEquipment, eqSpecification));
            terminalEquipmentNode.Expanded = false;

            // Add each terminal structure (card/tray/module)
            OutageSearchResult equipmentLevelResult = new OutageSearchResult();

            foreach (var terminalStructure in terminalEquipment.TerminalStructures.OrderBy(ts => ts.Position))
            {
                OutageSearchResult structureLevelResult = new OutageSearchResult();

                var terminalStructureSpecification = _terminalStructureSpecifications[terminalStructure.SpecificationId];

                var terminalStructureNode = new OutageViewNode(Guid.NewGuid(), terminalStructure.Name + " (" + terminalStructureSpecification.Name + ")");
                
                terminalStructureNode.Expanded = false;

                foreach (var terminal in terminalStructure.Terminals.Where(t => (t.Direction == TerminalDirectionEnum.BI || t.Direction == TerminalDirectionEnum.OUT)))
                {
                    var terminalLevelResult = AnalyzeCircuit(terminal.Id);

                    structureLevelResult.UnionWith(terminalLevelResult);

                    if (terminalLevelResult.InstallationOrCircuitsFound)
                    {
                        structureLevelResult.UnionWith(terminalLevelResult);

                        // Add terminal node
                        var terminalNode = new OutageViewNode(Guid.NewGuid(), terminal.Name, $"{terminalLevelResult.UniqueInstallationNamesFound} {{OutageInstallationsFound}} {{And}} {terminalLevelResult.UniqueCircuitNamesFound.Count} {{OutageCircuitsFound}}");
                        terminalNode.Expanded = false;
                        terminalStructureNode.AddNode(terminalNode);

                        // Now add all installations
                        foreach (var installationTerminalEquipment in terminalLevelResult.CustomerTerminationsFound)
                        {
                            var installationNode = new OutageViewNode(Guid.NewGuid(), installationTerminalEquipment.Name == null ? "NA" : installationTerminalEquipment.Name) { Value = installationTerminalEquipment.Name };
                            terminalNode.AddNode(installationNode);
                            processingState.InstallationNodes.Add((installationNode, installationTerminalEquipment));
                        }

                        // Now add all circuits
                        foreach (var circuitName in terminalLevelResult.UniqueCircuitNamesFound)
                        {
                            var circuitNode = new OutageViewNode(Guid.NewGuid(), circuitName == null ? "NA" : circuitName);
                            terminalNode.AddNode(circuitNode);
                        }
                    }
                }

                equipmentLevelResult.UnionWith(structureLevelResult);
          
                if (structureLevelResult.InstallationOrCircuitsFound)
                {
                    terminalStructureNode.Description = $"{structureLevelResult.UniqueInstallationNamesFound} {{OutageInstallationsFound}} {{And}} {structureLevelResult.UniqueCircuitNamesFound.Count} {{OutageCircuitsFound}}";
                    terminalEquipmentNode.AddNode(terminalStructureNode);
                }
            }

            rootNode.AddNode(terminalEquipmentNode);

            terminalEquipmentNode.Description = $"{equipmentLevelResult.UniqueInstallationNamesFound} {{OutageInstallationsFound}} {{And}} {equipmentLevelResult.UniqueCircuitNamesFound.Count} {{OutageCircuitsFound}}";
        }

        private static int CountUniqueInstallationNames(List<TerminalEquipment> installationEquipments)
        {
            HashSet<string> instByName = new HashSet<string>();

            foreach (var inst in installationEquipments)
                instByName.Add(inst.Name);

            int nInstallationsFoundTerminalLevel = instByName.Count;
            return nInstallationsFoundTerminalLevel;
        }

        private OutageViewNode GetOutageViewForRouteSegment(OutageProcessingState processingState)
        {
            _spanEquipmentSpecifications = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;

            _spanStructureSpecifications = _eventStore.Projections.Get<SpanStructureSpecificationsProjection>().Specifications;

            _terminalEquipmentSpecifications = _eventStore.Projections.Get<TerminalEquipmentSpecificationsProjection>().Specifications;


            OutageViewNode rootNode = new OutageViewNode(Guid.NewGuid(), "{OutageViewRouteSegment}");

            if (processingState.SpanEquipments == null)
            {
                rootNode.Description = "{OutageViewNoRelatedEquipmentsInRouteSegment}";
                return rootNode;
            }
            
            // Add conduits and related cables
            foreach (var outerConduit in processingState.SpanEquipments.Where(s => !s.IsCable))
            {
                var conduitSpecification = _spanEquipmentSpecifications[outerConduit.SpecificationId];

                var outerConduitNode = new OutageViewNode(Guid.NewGuid(), GetOuterConduitLabel(outerConduit, conduitSpecification));
                outerConduitNode.Expanded = false;
                outerConduitNode.InterestId = outerConduit.WalkOfInterestId;
                
                processingState.OuterConduitNodes.Add(outerConduitNode);

                OutageSearchResult conduitLevelResult = new OutageSearchResult();

                // If conduit is a multi conduit, then add each sub conduit
                if (conduitSpecification.IsMultiLevel)
                {
                    for (int i = 1; i < outerConduit.SpanStructures.Length; i++)
                    {
                        var innerConduit = outerConduit.SpanStructures[i];

                        var innerConduitSpecification = _spanStructureSpecifications[innerConduit.SpecificationId];

                        var innerConduitNode = new OutageViewNode(Guid.NewGuid(), GetInnerConduitLabel(innerConduit, innerConduitSpecification));
                        innerConduitNode.Expanded = true;

                        outerConduitNode.AddNode(innerConduitNode);

                        var subConduitLevelResult = AddRelatedCables(processingState, innerConduitNode, innerConduit);

                        innerConduitNode.Description = $"{subConduitLevelResult.CustomerTerminationsFound.Count} {{OutageInstallationsFound}} {{And}} {subConduitLevelResult.UniqueCircuitNamesFound.Count} {{OutageCircuitsFound}}";

                        conduitLevelResult.UnionWith(subConduitLevelResult);
                    }
                }
                else
                {
                    var subConduitLevelResult = AddRelatedCables(processingState, outerConduitNode, outerConduit.SpanStructures[0]);
                    conduitLevelResult.UnionWith(subConduitLevelResult);
                }

                outerConduitNode.Description = $"{conduitLevelResult.CustomerTerminationsFound.Count} {{OutageInstallationsFound}} {{And}} {conduitLevelResult.UniqueCircuitNamesFound.Count} {{OutageCircuitsFound}}";


                rootNode.AddNode(outerConduitNode);

            }


            // Add stand alone cables
            foreach (var cable in processingState.SpanEquipments.Where(s => s.IsCable))
            {
                if (!processingState.CableProcessed.Contains(cable.Id))
                {
                    var cableSpecification = _spanEquipmentSpecifications[cable.SpecificationId];

                    AddCable(processingState,rootNode, cable, cableSpecification);
                    processingState.CableProcessed.Add(cable.Id);
                }
            }

            AddAddressInformationToInstallations(processingState);

            AddNodeNamesToCablesAndOuterConduits(processingState);

            return rootNode;
        }

        private void AddNodeNamesToCablesAndOuterConduits(OutageProcessingState processingState)
        {
            if (processingState.CableNodes.Any()) {
                List<Guid> wois = new List<Guid>();

                foreach (var cable in processingState.CableNodes)
                {
                    wois.Add(cable.InterestId.Value);
                }

                foreach (var conduit in processingState.OuterConduitNodes)
                {
                    wois.Add(conduit.InterestId.Value);
                }

                var routeNetworkInfo = GatherRouteNetworkInformation(_queryDispatcher, wois);
                
                
                foreach (var cable in processingState.CableNodes)
                {
                    var cableWalk = routeNetworkInfo.Interests[cable.InterestId.Value];

                    var fromNode = routeNetworkInfo.RouteNetworkElements[cableWalk.RouteNetworkElementRefs.First()];
                    var toNode = routeNetworkInfo.RouteNetworkElements[cableWalk.RouteNetworkElementRefs.Last()];

                    if (fromNode.NamingInfo != null)
                    {
                        cable.Description += fromNode.NamingInfo.Name + " <-> ";
                    }

                    if (toNode.NamingInfo != null)
                    {
                        cable.Description += toNode.NamingInfo.Name;
                    }
                }

                foreach (var conduit in processingState.OuterConduitNodes)
                {
                    var walk = routeNetworkInfo.Interests[conduit.InterestId.Value];

                    var fromNode = routeNetworkInfo.RouteNetworkElements[walk.RouteNetworkElementRefs.First()];
                    var toNode = routeNetworkInfo.RouteNetworkElements[walk.RouteNetworkElementRefs.Last()];

                    if (fromNode.NamingInfo != null)
                    {
                        conduit.Label += " (" + fromNode.NamingInfo.Name + " <-> ";
                    }

                    if (toNode.NamingInfo != null)
                    {
                        conduit.Label += toNode.NamingInfo.Name + ")";
                    }
                }
            }
        }

        private void AddAddressInformationToInstallations(OutageProcessingState processingState)
        {
            HashSet<Guid> adresseIdsToFetch = new();

            foreach (var installation in processingState.InstallationNodes)
            {
                var installationAddressId = GetAddressIdFromTerminalEquipment(installation.Item2);

                if (installationAddressId != null)
                {
                    adresseIdsToFetch.Add(installationAddressId.Value);
                }
            }

            if (adresseIdsToFetch.Count > 0)
            {
                var addresses = GatherAddressInformation(adresseIdsToFetch);

                foreach (var installation in processingState.InstallationNodes)
                {
                    var installationAddressId = GetAddressIdFromTerminalEquipment(installation.Item2);

                    if (installationAddressId != null && addresses.ContainsKey(installationAddressId.Value))
                    {
                        installation.Item1.Description = addresses[installationAddressId.Value];
                    }
                }
            }
        }

        private Guid? GetAddressIdFromTerminalEquipment(TerminalEquipment terminalEquipment)
        {
            if (terminalEquipment.AddressInfo != null && terminalEquipment.AddressInfo.UnitAddressId != null)
                return terminalEquipment.AddressInfo.UnitAddressId.Value;
            else if (terminalEquipment.AddressInfo != null && terminalEquipment.AddressInfo.AccessAddressId != null)
                return terminalEquipment.AddressInfo.AccessAddressId.Value;

            return null;
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


        private OutageSearchResult AddRelatedCables(OutageProcessingState processingState, OutageViewNode innerConduitNode, SpanStructure innerConduit)
        {
            OutageSearchResult cableParentResult = new OutageSearchResult();

            // Find related cables places in inner conduits
            foreach (var spanSegment in innerConduit.SpanSegments)
            {
                if (_utilityNetwork.RelatedCablesByConduitSegmentId.ContainsKey(spanSegment.Id))
                {
                    foreach (var relatedCableId in _utilityNetwork.RelatedCablesByConduitSegmentId[spanSegment.Id])
                    {
                        if (_utilityNetwork.TryGetEquipment<SpanEquipment>(relatedCableId, out var cable))
                        {
                            var cableSpecification = _spanEquipmentSpecifications[cable.SpecificationId];

                            // Make sure that cable pass route element of interest
                            if (processingState.SpanEquipments.ContainsKey(cable.Id))
                            {
                                if (!processingState.CableProcessed.Contains(cable.Id))
                                {
                                    var cableLevelResult = AddCable(processingState, innerConduitNode, cable, cableSpecification);

                                    cableParentResult.UnionWith(cableLevelResult);

                                    processingState.CableProcessed.Add(cable.Id);
                                }
                            }
                        }
                    }
                }
            }

            return cableParentResult;
        }

        private OutageSearchResult AddCable(OutageProcessingState processingState, OutageViewNode parentNode, SpanEquipment cable, SpanEquipmentSpecification cableSpecification)
        {
            var cableNode = new OutageViewNode(cable.Id, GetCableLabel(cable, cableSpecification));
            cableNode.InterestId = cable.WalkOfInterestId;

            cableNode.Expanded = false;

            processingState.CableNodes.Add(cableNode);

            parentNode.AddNode(cableNode);

            HashSet<Guid> nInstallations = new();

            OutageSearchResult cableLevelOutageResult = new OutageSearchResult();

            // Trace all fibers to find eventually customers or circuits
            for (int fiberNumber = 0; fiberNumber < cable.SpanStructures.Count(); fiberNumber++)
            {
                var fiberStructure = cable.SpanStructures[fiberNumber];

                var fiberLevelResult = AnalyzeCircuit(fiberStructure.SpanSegments.First().Id);

                if (fiberLevelResult.InstallationOrCircuitsFound)
                {
                    // First add fiber node
                    var fiberNode = new OutageViewNode(Guid.NewGuid(), $"{{FiberNumber}}" + fiberNumber);
                    cableNode.AddNode(fiberNode);

                    // Now add all installations
                    foreach (var installationTerminalEquipment in fiberLevelResult.CustomerTerminationsFound)
                    {
                        nInstallations.Add(installationTerminalEquipment.Id);

                        var installationNode = new OutageViewNode(Guid.NewGuid(), installationTerminalEquipment.Name == null ? "NA" : installationTerminalEquipment.Name) { Value = installationTerminalEquipment.Name };
                        fiberNode.AddNode(installationNode);
                        processingState.InstallationNodes.Add((installationNode, installationTerminalEquipment));
                    }

                    // Now add all circuits
                    foreach (var circuitName in fiberLevelResult.UniqueCircuitNamesFound)
                    {
                        var circuitNode = new OutageViewNode(Guid.NewGuid(), circuitName == null ? "NA" : circuitName);
                        fiberNode.AddNode(circuitNode);
                    }
                }


                cableLevelOutageResult.UnionWith(fiberLevelResult);
            }

            return cableLevelOutageResult;
        }

        private OutageSearchResult AnalyzeCircuit(Guid fiberNetworkGraphElementId)
        {
            OutageSearchResult result = new();

            var traceResult = _utilityNetwork.Graph.OutageViewTrace(fiberNetworkGraphElementId);

            if (traceResult != null && traceResult.All.Count > 0)
            {
                foreach (var trace in traceResult.All)
                {
                    if (trace is IUtilityGraphTerminalRef)
                    {
                        var terminalRef = (IUtilityGraphTerminalRef)trace;

                        if (!terminalRef.IsDummyEnd)
                        {
                            var terminalEquipment = terminalRef.TerminalEquipment(_utilityNetwork);

                            var terminalEquipmentSpecification = _terminalEquipmentSpecifications[terminalEquipment.SpecificationId];

                            if (terminalEquipmentSpecification.IsCustomerTermination)
                            {
                                result.CustomerTerminationsFound.Add(terminalEquipment);
                            }

                            var terminalStructure = terminalRef.TerminalStructure(_utilityNetwork);

                            if (terminalStructure.interfaceInfo != null)
                            {
                                result.CircuitsFound.Add(terminalStructure.interfaceInfo);

                                result.UniqueCircuitNamesFound.Add(terminalStructure.interfaceInfo.CircuitName ?? Guid.NewGuid().ToString());
                            }

                        }
                    }
                }
            }

            return result;
        }

        private string GetCableLabel(SpanEquipment cable, SpanEquipmentSpecification cableSpecification)
        {
            return $"{cableSpecification.Name}";
        }

        private string GetInnerConduitLabel(SpanStructure innerConduit, SpanStructureSpecification innerConduitSpecification)
        {
            return $"{{InnerConduit}} {innerConduit.Position} ({{{innerConduitSpecification.Color}}})";
        }

        private string GetOuterConduitLabel(SpanEquipmentWithRelatedInfo outerConduit, SpanEquipmentSpecification spanEquipmentSpecification)
        {
            var label = spanEquipmentSpecification.Name;

            if (outerConduit.MarkingInfo != null && outerConduit.MarkingInfo.MarkingColor != null)
                label += $"({outerConduit.MarkingInfo.MarkingColor})";

            return label;
        }

        private string GetTerminalEquipmentLabel(TerminalEquipment terminalEquipment, TerminalEquipmentSpecification terminalEquipmentSpecification)
        {
            var label = terminalEquipment.Name + " (" + terminalEquipmentSpecification.Name + ")";

            return label;
        }

        public Result<OutageProcessingState> GetRouteNetworkElementEquipmentOfInterest(Guid routeNetworkElementId)
        {
            // Query all interests related to route network element
            var routeNetworkInterestQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { routeNetworkElementId })
            {
                RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
            };

            Result<GetRouteNetworkDetailsResult> interestsQueryResult = _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkInterestQuery).Result;

            if (interestsQueryResult.IsFailed)
                return Result.Fail(interestsQueryResult.Errors.First());

            OutageProcessingState result = new OutageProcessingState(routeNetworkElementId, interestsQueryResult.Value.RouteNetworkElements[routeNetworkElementId].Kind == RouteNetworkElementKindEnum.RouteNode);

            if (interestsQueryResult.Value.Interests == null)
                return Result.Ok(result);

            if (interestsQueryResult.Value.Interests.Count == 0)
                return Result.Ok(result);

            // Find equipments by interest ids
            var interestIdList = new InterestIdList();
            interestIdList.AddRange(interestsQueryResult.Value.Interests.Select(r => r.Id));

            var equipmentQueryResult = _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(interestIdList)
                {
                    EquipmentDetailsFilter = new EquipmentDetailsFilterOptions() { IncludeRouteNetworkTrace = false }
                }
            ).Result;

            if (equipmentQueryResult.IsFailed)
                return Result.Fail(equipmentQueryResult.Errors.First());
          

            if (equipmentQueryResult.Value.SpanEquipment != null)
            {
                result.SpanEquipments = equipmentQueryResult.Value.SpanEquipment;
            }


            var equipmentIdList = new EquipmentIdList();

            // Get all terminal equipments within node
            if (equipmentQueryResult.Value.NodeContainers != null && equipmentQueryResult.Value.NodeContainers.Count == 1)
            {
                result.NodeContainer = equipmentQueryResult.Value.NodeContainers.First();
                
                if (result.NodeContainer.TerminalEquipmentReferences != null)
                {
                    equipmentIdList.AddRange(result.NodeContainer.TerminalEquipmentReferences);
                }
            }

            // Add equipments in racks as well
            if (result.NodeContainer != null && result.NodeContainer.Racks != null)
            {
                foreach (var rack in result.NodeContainer.Racks)
                {
                    foreach (var subRack in rack.SubrackMounts)
                    {
                        equipmentIdList.Add(subRack.TerminalEquipmentId);
                    }
                }
            }

            if (equipmentIdList.Count > 0)
            {
                equipmentQueryResult = _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                     new GetEquipmentDetails(equipmentIdList)
                     {
                         EquipmentDetailsFilter = new EquipmentDetailsFilterOptions() { IncludeRouteNetworkTrace = false }
                     }
                 ).Result;

                if (equipmentQueryResult.IsFailed)
                    return Result.Fail(equipmentQueryResult.Errors.First());

                if (equipmentQueryResult.Value.TerminalEquipment != null)
                {
                    result.TerminalEquipments = equipmentQueryResult.Value.TerminalEquipment;
                }
            }

            return Result.Ok(result);
        }

        private static GetRouteNetworkDetailsResult GatherRouteNetworkInformation(IQueryDispatcher queryDispatcher, IEnumerable<Guid> walkOfInterestIds)
        {
            InterestIdList interestIdList = new();
            interestIdList.AddRange(walkOfInterestIds);

            var interestQueryResult = queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
                new GetRouteNetworkDetails(interestIdList)
                {
                    RouteNetworkElementFilter = new RouteNetworkElementFilterOptions() { IncludeNamingInfo = true, IncludeCoordinates = false }
                }
            ).Result;

            if (interestQueryResult.IsFailed)
                throw new ApplicationException("Failed to query route network information. Got error: " + interestQueryResult.Errors.First().Message);

            return interestQueryResult.Value;
        }

        public class OutageProcessingState
        {
            public Guid RouteElementId { get; }
            public bool IsNode { get; }
            public NodeContainer? NodeContainer { get; set; }
            public LookupCollection<SpanEquipmentWithRelatedInfo> SpanEquipments { get; set; }

            public LookupCollection<TerminalEquipment> TerminalEquipments { get; set; }

            public HashSet<Guid> CableProcessed = new();

            public List<(OutageViewNode,TerminalEquipment)> InstallationNodes = new();

            public List<OutageViewNode> CableNodes = new();

            public List<OutageViewNode> OuterConduitNodes = new();

            public OutageProcessingState(Guid routeElementId, bool isNode)
            {
                RouteElementId = routeElementId;
                IsNode = isNode;
                SpanEquipments = new LookupCollection<SpanEquipmentWithRelatedInfo>();
            }

            public bool AnyEquipment(Guid equipmentId)
            {
                if (IsRackEquipment(equipmentId) || IsTerminalEquipment(equipmentId)) 
                    return true;

                return false;
            }

            public bool IsRackEquipment(Guid equipmentId)
            {
                if (NodeContainer != null && NodeContainer.Racks != null && NodeContainer.Racks.Any(r => r.Id == equipmentId))
                    return true;

                return false;
            }
            public bool IsTerminalEquipment(Guid equipmentId)
            {
                if (TerminalEquipments.ContainsKey(equipmentId))
                    return true;

                return false;
            }
        }

        public class OutageSearchResult
        {
            public List<TerminalEquipment> CustomerTerminationsFound { get; set; }

            public List<InterfaceInfo> CircuitsFound { get; set; }

            public HashSet<string> UniqueCircuitNamesFound { get; set; }

            public OutageSearchResult()
            {
                CustomerTerminationsFound = new List<TerminalEquipment>();
                CircuitsFound = new List<InterfaceInfo>();
                UniqueCircuitNamesFound = new HashSet<string>();
            }

            public void UnionWith(OutageSearchResult other)
            {
                this.CustomerTerminationsFound.AddRange(other.CustomerTerminationsFound);
                this.CircuitsFound.AddRange(other.CircuitsFound);
                this.UniqueCircuitNamesFound.UnionWith(other.UniqueCircuitNamesFound);
            }

            public int UniqueInstallationNamesFound
            {
                get
                {
                    return new HashSet<string>(CustomerTerminationsFound.Where(c => c.Name != null).Select(c => c.Name)).Count;
                }
            }


            public bool InstallationOrCircuitsFound
            {
                get
                {
                    if (CustomerTerminationsFound.Any() || UniqueCircuitNamesFound.Any())
                        return true;
                    else
                        return false;
                }
            }
        }

    }
}
