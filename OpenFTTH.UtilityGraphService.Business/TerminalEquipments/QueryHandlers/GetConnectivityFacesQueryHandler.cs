using FluentResults;
using OpenFTTH.Address.API.Model;
using OpenFTTH.Address.API.Queries;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.Trace;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.NodeContainers.Projections;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Projections;
using OpenFTTH.UtilityGraphService.Business.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.TerminalEquipments.QueryHandling
{
    public class GetConnectivityFacesQueryHandler
        : IQueryHandler<GetConnectivityFaces, Result<List<ConnectivityFace>>>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly UtilityNetworkProjection _utilityNetwork;
        private LookupCollection<RackSpecification> _rackSpecifications;
        private LookupCollection<TerminalStructureSpecification> _terminalStructureSpecifications;
        private LookupCollection<TerminalEquipmentSpecification> _terminalEquipmentSpecifications;
        private LookupCollection<SpanEquipmentSpecification> _spanEquipmentSpecifications;

        public GetConnectivityFacesQueryHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
        }

        public Task<Result<List<ConnectivityFace>>> HandleAsync(GetConnectivityFaces query)
        {
            _rackSpecifications = _eventStore.Projections.Get<RackSpecificationsProjection>().Specifications;
            _terminalStructureSpecifications = _eventStore.Projections.Get<TerminalStructureSpecificationsProjection>().Specifications;
            _terminalEquipmentSpecifications = _eventStore.Projections.Get<TerminalEquipmentSpecificationsProjection>().Specifications;
            _spanEquipmentSpecifications = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;

            var relatedEquipmentsResult = FetchRelatedEquipments(_queryDispatcher, query.routeNodeId);

            if (relatedEquipmentsResult.IsFailed)
                return Task.FromResult(Result.Fail<List<ConnectivityFace>>(relatedEquipmentsResult.Errors.First()));

            return Task.FromResult(Result.Ok(BuildConnectivityFaces(relatedEquipmentsResult.Value)));
        }

        private List<ConnectivityFace> BuildConnectivityFaces(RouteNetworkElementRelatedData data)
        {
            List<ConnectivityFace> connectivityFacesResult = new();

            // Add cable span equipments
            if (data.SpanEquipments != null)
            {
                foreach (var cableSpanEquipment in data.SpanEquipments.Where(s => s.IsCable))
                {
                    var spanEquipmentSpecification = _spanEquipmentSpecifications[cableSpanEquipment.SpecificationId];


                    var relType = data.InterestRelations[cableSpanEquipment.WalkOfInterestId];

                    if (relType.RelationKind != RouteNetworkInterestRelationKindEnum.PassThrough && relType.RelationKind != RouteNetworkInterestRelationKindEnum.InsideNode)
                    {
                        var trace = data.RouteNetworkTraces[data.SpanEquipments[cableSpanEquipment.Id].RouteNetworkTraceRefs.First().TraceId];

                        connectivityFacesResult.Add(
                           new ConnectivityFace()
                           {
                               EquipmentId = cableSpanEquipment.Id,
                               EquipmentKind = ConnectivityEquipmentKindEnum.SpanEquipment,
                               FaceKind = FaceKindEnum.SpliceSide,
                               FaceName = GetFaceName(data, cableSpanEquipment, relType, trace),
                               EquipmentName = cableSpanEquipment.Name + " " + spanEquipmentSpecification.Name
                           }
                       );
                    }
                }
            }

            // Add terminal equipments
            if (data.TerminalEquipments != null)
            {
                foreach (var terminalEquipment in data.TerminalEquipments)
                {
                    var terminalEquipmentSpecification = _terminalEquipmentSpecifications[terminalEquipment.SpecificationId];

                    string? rackInfo = null;

                    if (terminalEquipmentSpecification.IsRackEquipment)
                    {
                        rackInfo = GetRackName(data, terminalEquipment.Id) + "-";
                    }

                    var equipmentName = terminalEquipmentSpecification.ShortName + " " + rackInfo + terminalEquipment.Name;

                    if (HasSpliceSide(terminalEquipment))
                    {
                        connectivityFacesResult.Add(
                            new ConnectivityFace()
                            {
                                EquipmentId = terminalEquipment.Id,
                                EquipmentKind = ConnectivityEquipmentKindEnum.TerminalEquipment,
                                FaceKind = FaceKindEnum.SpliceSide,
                                FaceName = "Splice Side",
                                EquipmentName = equipmentName
                            }
                        );
                    }

                    if (HasPatchSide(terminalEquipment))
                    {
                        connectivityFacesResult.Add(
                            new ConnectivityFace()
                            {
                                EquipmentId = terminalEquipment.Id,
                                EquipmentKind = ConnectivityEquipmentKindEnum.TerminalEquipment,
                                FaceKind = FaceKindEnum.PatchSide,
                                FaceName = "Patch Side",
                                EquipmentName = equipmentName
                            }
                        );
                    }
                }
            }

            return connectivityFacesResult;
        }

        private string GetFaceName(RouteNetworkElementRelatedData data, SpanEquipment cable, RouteNetworkElementInterestRelation relType, RouteNetworkTraceResult trace)
        {
            var endRouteNodeName = relType.RelationKind == RouteNetworkInterestRelationKindEnum.Start ? trace.ToRouteNodeName : trace.FromRouteNodeName;

            if (string.IsNullOrEmpty(endRouteNodeName))
            {
                // try find id any related conduit adresse
                if (data.CableToRelatedConduitAddressRelations.ContainsKey(cable.Id))
                {
                    var addressId = data.CableToRelatedConduitAddressRelations[cable.Id];
                    if (data.AddressStrings.ContainsKey(addressId))
                        endRouteNodeName = data.AddressStrings[addressId];
                }
            }

            return "Mod " + endRouteNodeName;
        }

        private string? GetRackName(RouteNetworkElementRelatedData data, Guid equipmentId)
        {
            if (data.NodeContainer != null && data.NodeContainer.Racks != null)
            {
                foreach (var rack in data.NodeContainer.Racks)
                {
                    foreach (var rackMount in rack.SubrackMounts)
                    {
                        if (rackMount.TerminalEquipmentId == equipmentId)
                            return rack.Name;
                    }
                }
            }

            return null;
        }

        private bool HasSpliceSide(TerminalEquipment terminalEquipment)
        {
            foreach (var structure in terminalEquipment.TerminalStructures.Where(t => !t.Deleted))
            {
                foreach (var terminal in structure.Terminals)
                {
                    if (terminal.IsSplice)
                        return true;
                }
            }

            return false;
        }

        private bool HasPatchSide(TerminalEquipment terminalEquipment)
        {
            foreach (var structure in terminalEquipment.TerminalStructures.Where(t => !t.Deleted))
            {
                foreach (var terminal in structure.Terminals)
                {
                    if (terminal.ConnectorType != null)
                        return true;
                }
            }

            return false;
        }


        private Result<RouteNetworkElementRelatedData> FetchRelatedEquipments(IQueryDispatcher queryDispatcher, Guid routeNetworkElementId)
        {
            RouteNetworkElementRelatedData result = new RouteNetworkElementRelatedData();

            result.RouteNetworkElementId = routeNetworkElementId;

            // Query all route node interests
            var routeNetworkInterestQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { routeNetworkElementId })
            {
                RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
            };

            Result<GetRouteNetworkDetailsResult> interestsQueryResult = queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkInterestQuery).Result;

            if (interestsQueryResult.IsFailed)
                return Result.Fail(interestsQueryResult.Errors.First());

            result.InterestRelations = interestsQueryResult.Value.RouteNetworkElements.First().InterestRelations.ToDictionary(r => r.RefId);

            result.RouteNetworkInterests = interestsQueryResult.Value.Interests;

            var interestIdList = new InterestIdList();
            interestIdList.AddRange(result.InterestRelations.Values.Select(r => r.RefId));

            // Only query for equipments if interests are returned from the route network query
            if (interestIdList.Count > 0)
            {
                // Query all the equipments related to the route network element
                var equipmentQueryResult = queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                    new GetEquipmentDetails(interestIdList)
                    {
                        EquipmentDetailsFilter = new EquipmentDetailsFilterOptions() { IncludeRouteNetworkTrace = true }
                    }
                ).Result;

                if (equipmentQueryResult.IsFailed)
                    return Result.Fail(equipmentQueryResult.Errors.First());

                result.SpanEquipments = equipmentQueryResult.Value.SpanEquipment;
                result.RouteNetworkTraces = equipmentQueryResult.Value.RouteNetworkTraces;

                if (equipmentQueryResult.Value.NodeContainers != null && equipmentQueryResult.Value.NodeContainers.Count > 0)
                {
                    result.NodeContainer = equipmentQueryResult.Value.NodeContainers.First();
                    result.NodeContainerRouteNetworkElementId = interestsQueryResult.Value.Interests[result.NodeContainer.InterestId].RouteNetworkElementRefs[0];
                }

                // Query all route network elements of all the equipments
                var routeNetworkElementsQuery = new GetRouteNetworkDetails(interestIdList);
                Result<GetRouteNetworkDetailsResult> routeElementsQueryResult = queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkElementsQuery).Result;

                result.RouteNetworkElements = routeElementsQueryResult.Value.RouteNetworkElements;
            }
            else
            {
                result.RouteNetworkElements = new LookupCollection<RouteNetworkElement>();
                result.SpanEquipments = new LookupCollection<SpanEquipmentWithRelatedInfo>();
            }

            // Query terminal equipments
            List<Guid> terminalEquipmentIds = new();

            if (result.NodeContainer != null)
            {
                if (result.NodeContainer.Racks != null)
                {
                    foreach (var rack in result.NodeContainer.Racks)
                    {
                        foreach (var mount in rack.SubrackMounts)
                            terminalEquipmentIds.Add(mount.TerminalEquipmentId);
                    }
                }

                if (result.NodeContainer.TerminalEquipmentReferences != null)
                {
                    foreach (var terminalEquipmentReference in result.NodeContainer.TerminalEquipmentReferences)
                    {
                        terminalEquipmentIds.Add(terminalEquipmentReference);
                    }
                }
            }

            if (terminalEquipmentIds.Count > 0)
            {
                var terminalEquipmentQueryResult = queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                    new GetEquipmentDetails(new EquipmentIdList(terminalEquipmentIds))
                ).Result;

                if (terminalEquipmentQueryResult.IsFailed)
                    return Result.Fail(terminalEquipmentQueryResult.Errors.First());

                result.TerminalEquipments = terminalEquipmentQueryResult.Value.TerminalEquipment;
            }


            // Find related conduits addresses
            result.CableToRelatedConduitAddressRelations = new();

            HashSet<Guid> adresseIdsToQuery = new();
                        
            if (result.SpanEquipments != null)
            {
                foreach (var spanEquipment in result.SpanEquipments)
                {
                    if (spanEquipment.IsCable && spanEquipment.UtilityNetworkHops != null)
                    {
                        foreach (var utilityNetworkHop in spanEquipment.UtilityNetworkHops)
                        {
                            foreach (var parentAffix in utilityNetworkHop.ParentAffixes)
                            {
                                if (_utilityNetwork.TryGetEquipment<SpanEquipment>(parentAffix.SpanSegmentId, out var parentEquipment))
                                {
                                    if (parentEquipment.AddressInfo != null && parentEquipment.AddressInfo.UnitAddressId != null)
                                    {
                                        if (!result.CableToRelatedConduitAddressRelations.ContainsKey(spanEquipment.Id))
                                        {
                                            adresseIdsToQuery.Add(parentEquipment.AddressInfo.UnitAddressId.Value);
                                            result.CableToRelatedConduitAddressRelations.Add(spanEquipment.Id, parentEquipment.AddressInfo.UnitAddressId.Value);
                                        }
                                    }
                                    else if (parentEquipment.AddressInfo != null && parentEquipment.AddressInfo.AccessAddressId != null)
                                    {
                                        if (!result.CableToRelatedConduitAddressRelations.ContainsKey(spanEquipment.Id))
                                        {
                                            adresseIdsToQuery.Add(parentEquipment.AddressInfo.AccessAddressId.Value);
                                            result.CableToRelatedConduitAddressRelations.Add(spanEquipment.Id, parentEquipment.AddressInfo.AccessAddressId.Value);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            result.AddressStrings = GatherAddressInformation(adresseIdsToQuery);


            return Result.Ok(result);
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

        public class RouteNetworkElementRelatedData
        {
            public Guid RouteNetworkElementId { get; set; }
            public LookupCollection<RouteNetworkElement> RouteNetworkElements { get; set; }
            public LookupCollection<RouteNetworkInterest> RouteNetworkInterests { get; set; }
            public LookupCollection<SpanEquipmentWithRelatedInfo> SpanEquipments { get; set; }
            public LookupCollection<TerminalEquipment> TerminalEquipments { get; set; }
            public LookupCollection<API.Model.Trace.RouteNetworkTraceResult> RouteNetworkTraces { get; set; }
            public Dictionary<Guid, RouteNetworkElementInterestRelation> InterestRelations { get; set; }
            public Dictionary<Guid, string> AddressStrings { get; set; }
            public Dictionary<Guid, Guid> CableToRelatedConduitAddressRelations { get; set; }
            public NodeContainer NodeContainer { get; set; }
            public Guid NodeContainerRouteNetworkElementId { get; set; }
        }
    }
}
