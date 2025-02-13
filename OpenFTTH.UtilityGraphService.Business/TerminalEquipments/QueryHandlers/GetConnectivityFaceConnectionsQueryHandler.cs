using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model;
using OpenFTTH.UtilityGraphService.API.Model.Trace;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.Graph.Trace;
using OpenFTTH.UtilityGraphService.Business.NodeContainers.Projections;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Projections;
using OpenFTTH.UtilityGraphService.Business.Trace.Util;
using OpenFTTH.UtilityGraphService.Business.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.TerminalEquipments.QueryHandling
{
    public class GetConnectivityFaceConnectionsQueryHandler
        : IQueryHandler<GetConnectivityFaceConnections, Result<List<ConnectivityFaceConnection>>>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly UtilityNetworkProjection _utilityNetwork;
        private LookupCollection<RackSpecification> _rackSpecifications;
        private LookupCollection<TerminalStructureSpecification> _terminalStructureSpecifications;
        private LookupCollection<TerminalEquipmentSpecification> _terminalEquipmentSpecifications;
        private LookupCollection<SpanEquipmentSpecification> _spanEquipmentSpecifications;

        public GetConnectivityFaceConnectionsQueryHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
        }

        public Task<Result<List<ConnectivityFaceConnection>>> HandleAsync(GetConnectivityFaceConnections query)
        {
            _rackSpecifications = _eventStore.Projections.Get<RackSpecificationsProjection>().Specifications;
            _terminalStructureSpecifications = _eventStore.Projections.Get<TerminalStructureSpecificationsProjection>().Specifications;
            _terminalEquipmentSpecifications = _eventStore.Projections.Get<TerminalEquipmentSpecificationsProjection>().Specifications;
            _spanEquipmentSpecifications = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;

            var relatedData = FetchRelatedEquipments(_queryDispatcher, query.routeNodeId).Value;

            if (_utilityNetwork.TryGetEquipment<TerminalEquipment>(query.spanOrTerminalEquipmentId, out var terminalEquipment))
            {
                return Task.FromResult(Result.Ok(BuildConnectivityFaceConnectionsForTerminalEquipment(terminalEquipment, query, relatedData)));
            }
            else if (_utilityNetwork.TryGetEquipment<SpanEquipment>(query.spanOrTerminalEquipmentId, out var spanEquipment))
            {
                return Task.FromResult(Result.Ok(BuildConnectivityFaceConnectionsForSpanEquipment(spanEquipment, query, relatedData)));
            }
            else
                return Task.FromResult(Result.Fail<List<ConnectivityFaceConnection>>(new GetEquipmentDetailsError(GetEquipmentDetailsErrorCodes.INVALID_QUERY_ARGUMENT_ERROR_LOOKING_UP_SPECIFIED_EQUIPMENT_BY_EQUIPMENT_ID, $"Cannot find any span or terminal equipment with id: {query.spanOrTerminalEquipmentId}")));
        }

        private List<ConnectivityFaceConnection> BuildConnectivityFaceConnectionsForSpanEquipment(SpanEquipment spanEquipment, GetConnectivityFaceConnections query, RouteNetworkElementRelatedData relatedData)
        {
            List<ConnectivityFaceConnection> connectivityFacesResult = new();

            for (int structureIndex = 1; structureIndex < spanEquipment.SpanStructures.Count(); structureIndex++)
            {
                connectivityFacesResult.Add(BuildConnectivityInfoForSpanSegment(spanEquipment, structureIndex, query, relatedData));
            }

            return connectivityFacesResult;
        }

        private ConnectivityFaceConnection BuildConnectivityInfoForSpanSegment(SpanEquipment spanEquipment, int structureIndex, GetConnectivityFaceConnections query, RouteNetworkElementRelatedData relatedData)
        {
            var spanEquipmentSpecification = _spanEquipmentSpecifications[spanEquipment.SpecificationId];

            // TODO: Support making breakout on cable
            var spanSegmentId = spanEquipment.SpanStructures[structureIndex].SpanSegments[0].Id;

            var spanSegmentTraceResult = _utilityNetwork.Graph.SimpleTrace(spanSegmentId);

            bool isConnected = CheckIfCableIsConnected(spanSegmentTraceResult, query.routeNodeId, query.FaceType);

            var numberOfFibers = spanEquipment.SpanStructures.Count() - 1;

            var equipmentName = spanEquipment.Name + " (" + numberOfFibers + ") - Fiber " + structureIndex;

            return new ConnectivityFaceConnection()
            {
                TerminalOrSegmentId = spanSegmentId,
                Name = equipmentName,
                EndInfo = null,
                IsConnected = isConnected
            };
        }

        private bool CheckIfCableIsConnected(UtilityGraphTraceResult spanSegmentTraceResult, Guid nodeId, FaceKindEnum directionType)
        {
            if (spanSegmentTraceResult.Upstream.Count() > 1)
            {
                var terminal = spanSegmentTraceResult.Upstream[1] as IUtilityGraphTerminalRef;

                if (terminal != null && terminal.RouteNodeId == nodeId && !terminal.IsDummyEnd)
                {
                    return true;
                }
            }

            if (spanSegmentTraceResult.Downstream.Count() > 1)
            {
                var terminal = spanSegmentTraceResult.Downstream[1] as IUtilityGraphTerminalRef;

                if (terminal != null && terminal.RouteNodeId == nodeId && !terminal.IsDummyEnd)
                {
                    return true;
                }
            }

            return false;

        }

        private List<ConnectivityFaceConnection> BuildConnectivityFaceConnectionsForTerminalEquipment(TerminalEquipment terminalEquipment, GetConnectivityFaceConnections query, RouteNetworkElementRelatedData relatedData)
        {
            List<ConnectivityFaceConnection> connectivityFacesResult = new();

            foreach (var terminalStructure in terminalEquipment.TerminalStructures.Where(t => !t.Deleted).OrderBy(o => o.Position))
            {
                foreach (var terminal in terminalStructure.Terminals)
                {
                    connectivityFacesResult.Add(BuildConnectivityInfoForTerminal(terminalEquipment, terminalStructure, terminal, query, relatedData));
                }
            }

            return connectivityFacesResult;
        }

        private ConnectivityFaceConnection BuildConnectivityInfoForTerminal(TerminalEquipment terminalEquipment, TerminalStructure terminalStructure, Terminal terminal, GetConnectivityFaceConnections query, RouteNetworkElementRelatedData relatedData)
        {
            var terminalTraceResult = _utilityNetwork.Graph.SimpleTrace(terminal.Id);

            bool isConnected = CheckIfTerminalIsConnected(terminalTraceResult, query.FaceType);

            var equipmentName = BuildEquipmentName(relatedData, terminal, terminalStructure, terminalEquipment);

            return new ConnectivityFaceConnection()
            {
                TerminalOrSegmentId = terminal.Id,
                Name = equipmentName,
                EndInfo = null,
                IsConnected = isConnected
            };
        }

        private string BuildEquipmentName(RouteNetworkElementRelatedData relatedData, Terminal terminal, TerminalStructure terminalStructure, TerminalEquipment terminalEquipment)
        {
            var terminalEquipmentSpecification = _terminalEquipmentSpecifications[terminalEquipment.SpecificationId];
            var terminalStructureSpecification = _terminalStructureSpecifications[terminalStructure.SpecificationId];

            string? rackInfo = null;

            if (terminalEquipmentSpecification.IsRackEquipment)
            {
                rackInfo = GetRackName(relatedData, terminalEquipment.Id) + " - ";
            }

            if (terminalStructure.interfaceInfo == null)
            {

                if (terminalStructureSpecification.Category == "Splitters")
                {
                    return rackInfo + "Splitter " + terminalStructure.Name + " (" + terminalStructureSpecification.ShortName + ") - " + terminal.Name;
                }
                else
                {
                    return rackInfo + "Tray " + terminalStructure.Name + " - Søm " + terminal.Name + " (" + terminalEquipmentSpecification.ShortName + ")";
                }
            }
            else
            {
                return rackInfo + RelatedDataHolder.GetInterfaceName(terminalStructure);
            }
        }

        private bool CheckIfTerminalIsConnected(UtilityGraphTraceResult traceResult, FaceKindEnum directionType)
        {
            if (directionType == FaceKindEnum.SpliceSide && CheckIfTerminalIsSpliced(traceResult))
                return true;

            else if (directionType == FaceKindEnum.PatchSide && CheckIfTerminalIsPatched(traceResult))
                return true;

            else
                return false;
        }

        private bool CheckIfTerminalIsPatched(UtilityGraphTraceResult traceResult)
        {
            if (CheckIfTerminalIsPatchedUpstream(traceResult))
                return true;

            if (CheckIfTerminalIsPatchedDownstream(traceResult))
                return true;

            return false;
        }

        private bool CheckIfTerminalIsSpliced(UtilityGraphTraceResult traceResult)
        {
            // If patch splice, only one side has to be spliced for terminal to be fully spliced
            if (CheckIfTerminalIsSplicePatch(traceResult.TerminalOrSpanSegmentId))
            {
                if (CheckIfTerminalIsSplicedUpstream(traceResult) || CheckIfTerminalIsSplicedDownstream(traceResult))
                    return true;
            }
            // If splice pin, boths sides has to be spliced for terminal to be fully spliced
            else
            {
                if (CheckIfTerminalIsSplicedUpstream(traceResult) && CheckIfTerminalIsSplicedDownstream(traceResult))
                    return true;
            }

            return false;
        }

        private bool CheckIfTerminalIsSplicePatch(Guid terminalId)
        {
            if (_utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphTerminalRef>(terminalId, out var terminalRef))
            {
                var structureSpec = _terminalStructureSpecifications[terminalRef.TerminalStructure(_utilityNetwork).SpecificationId];
                if (structureSpec.TerminalTemplates[terminalRef.TerminalIndex].ConnectorType != null)
                    return true;
            }

            return false;
        }

        private bool CheckIfTerminalIsPatchedUpstream(UtilityGraphTraceResult traceResult)
        {
            if (traceResult.Upstream.Count() > 0)
            {
                if (traceResult.Upstream.First() is UtilityGraphConnectedSegment connectedSegment)
                {
                    if (connectedSegment.IsPatch)
                    return true; 
                }
            }

            return false;
        }

        private bool CheckIfTerminalIsPatchedDownstream(UtilityGraphTraceResult traceResult)
        {
            if (traceResult.Downstream.Count() > 0)
            {
                var firstConnection = traceResult.Downstream.First();
                
                if (firstConnection is UtilityGraphConnectedSegment connectedSegment)
                {
                    if (connectedSegment.IsPatch)
                        return true;
                }

                if (firstConnection is UtilityGraphTerminalToTerminalConnectivityLink)
                {
                        return true;
                }
            }

            return false;
        }

        private bool CheckIfTerminalIsSplicedUpstream(UtilityGraphTraceResult traceResult)
        {
            if (traceResult.Upstream.Count() > 0)
            {
                if (traceResult.Upstream.First() is UtilityGraphConnectedSegment connectedSegment)
                {
                    if (!connectedSegment.IsPatch)
                        return true;
                }
            }

            return false;
        }

        private bool CheckIfTerminalIsSplicedDownstream(UtilityGraphTraceResult traceResult)
        {
            if (traceResult.Downstream.Count() > 0)
            {
                if (traceResult.Downstream.First() is UtilityGraphConnectedSegment connectedSegment)
                {
                    if (!connectedSegment.IsPatch)
                        return true;
                }
            }

            return false;
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

        private static Result<RouteNetworkElementRelatedData> FetchRelatedEquipments(IQueryDispatcher queryDispatcher, Guid routeNetworkElementId)
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

            return Result.Ok(result);
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
            public NodeContainer NodeContainer { get; set; }
            public Guid NodeContainerRouteNetworkElementId { get; set; }
        }



    }
}
