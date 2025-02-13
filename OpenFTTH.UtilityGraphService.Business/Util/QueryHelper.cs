using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.UtilityGraphService.Business.Util
{
    public static class QueryHelper
    {
        public static Result<NodeContainer> GetNodeContainerFromRouteNodeId(IQueryDispatcher queryDispatcher, Guid routeNodeId)
        {
            // Query all route node interests
            var routeNetworkInterestQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { routeNodeId })
            {
                RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementOnly
            };

            Result<GetRouteNetworkDetailsResult> interestsQueryResult = queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkInterestQuery).Result;

            if (interestsQueryResult.IsFailed)
                return Result.Fail(interestsQueryResult.Errors.First());

            var interestIdList = new InterestIdList();
            interestIdList.AddRange(interestsQueryResult.Value.RouteNetworkElements[routeNodeId].InterestRelations.Select(r => r.RefId));

            // Only query for equipments if interests are returned from the route network query
            if (interestIdList.Count > 0)
            {
                // Query all the equipments related to the route network element
                var equipmentQueryResult = queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                    new GetEquipmentDetails(interestIdList)
                    {
                        EquipmentDetailsFilter = new EquipmentDetailsFilterOptions() { IncludeRouteNetworkTrace = false }
                    }
                ).Result;

                if (equipmentQueryResult.IsFailed)
                    return Result.Fail(equipmentQueryResult.Errors.First());

                if (equipmentQueryResult.Value.NodeContainers != null && equipmentQueryResult.Value.NodeContainers.Count > 0)
                {
                    return Result.Ok(equipmentQueryResult.Value.NodeContainers.First());
                }
            }

            return Result.Fail(new Error($"Failed to find node container in route node with id: {routeNodeId}"));
        }

        public static Result<Dictionary<Guid,NodeContainer>> GetNodeContainersFromRouteNodeIds(IQueryDispatcher queryDispatcher, Guid[] routeNodeIds)
        {
            Dictionary<Guid, NodeContainer> result = new();

            var idsToQuery = new RouteNetworkElementIdList();
            idsToQuery.AddRange(routeNodeIds);

            // Query all route node interests
            var routeNetworkInterestQuery = new GetRouteNetworkDetails(idsToQuery)
            {
                RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementOnly
            };

            Result<GetRouteNetworkDetailsResult> interestsQueryResult = queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNetworkInterestQuery).Result;

            if (interestsQueryResult.IsFailed)
                return Result.Fail(interestsQueryResult.Errors.First());

            var interestIdList = new InterestIdList();

            if (interestsQueryResult.Value.RouteNetworkElements != null)
            {
                foreach (var routeNetworkElement in interestsQueryResult.Value.RouteNetworkElements)
                {
                    if (routeNetworkElement.InterestRelations != null)
                        interestIdList.AddRange(routeNetworkElement.InterestRelations.Select(r => r.RefId));
                }
            }

            // Only query for equipments if interests are returned from the route network query
            if (interestIdList.Count > 0)
            {
                // Query all the equipments related to the route network element
                var equipmentQueryResult = queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                    new GetEquipmentDetails(interestIdList)
                    {
                        EquipmentDetailsFilter = new EquipmentDetailsFilterOptions() { IncludeRouteNetworkTrace = false }
                    }
                ).Result;

                if (equipmentQueryResult.IsFailed)
                    return Result.Fail(equipmentQueryResult.Errors.First());

                if (equipmentQueryResult.Value.NodeContainers != null && equipmentQueryResult.Value.NodeContainers.Count > 0)
                {
                    

                    foreach (var nodeContainer in equipmentQueryResult.Value.NodeContainers)
                        result.Add(nodeContainer.Id, nodeContainer);
                }
            }

            return Result.Ok(result);
        }


        public static ValidatedRouteNetworkWalk GetWalkOfInterest(IQueryDispatcher queryDispatcher, SpanEquipment spanEquipment)
        {
            // Get interest information from existing span equipment
            var interestQueryResult = queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
                new GetRouteNetworkDetails(new InterestIdList() { spanEquipment.WalkOfInterestId })
                {
                    RouteNetworkElementFilter = new RouteNetworkElementFilterOptions() { IncludeCoordinates = false, IncludeNamingInfo = false },
                    RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
                }
            ).Result;

            if (interestQueryResult.IsFailed)
                throw new ApplicationException($"Got an unexpected error querying walk of interest of span equipment with id: {spanEquipment.Id}: {interestQueryResult.Errors.First().Message}");

            if (interestQueryResult.Value.Interests == null)
                throw new ApplicationException($"Error querying interest information belonging to span equipment with id: {spanEquipment.Id}. No interest information returned from route network service.");

            if (!interestQueryResult.Value.Interests.TryGetValue(spanEquipment.WalkOfInterestId, out var routeNetworkInterest))
                throw new ApplicationException($"Error querying interest information belonging to span equipment with id: {spanEquipment.Id}. No interest information returned from route network service.");

            return new ValidatedRouteNetworkWalk(routeNetworkInterest.RouteNetworkElementRefs);
        }

        public static void GetWalkAndRouteNetworkElementsByInterestId(IQueryDispatcher queryDispatcher, Guid interestId, out ValidatedRouteNetworkWalk walk, out LookupCollection<RouteNetworkElement> routeNetworkElements)
        {
            // Get interest information from existing span equipment
            var interestQueryResult = queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
                new GetRouteNetworkDetails(new InterestIdList() { interestId })
            ).Result;

            if (interestQueryResult.IsFailed)
                throw new ApplicationException($"Got an unexpected error querying route network element by interest id: {interestId}: {interestQueryResult.Errors.First().Message}");

            if (interestQueryResult.Value.Interests == null)
                throw new ApplicationException($"Error querying interest information belonging interest with id: {interestId}. No interest information returned from route network service.");

            if (!interestQueryResult.Value.Interests.TryGetValue(interestId, out var routeNetworkInterest))
                throw new ApplicationException($"Error querying interest information belonging interest with id: {interestId}. No interest information returned from route network service.");

            walk = new ValidatedRouteNetworkWalk(routeNetworkInterest.RouteNetworkElementRefs);

            if (interestQueryResult.Value.RouteNetworkElements != null)
                routeNetworkElements = interestQueryResult.Value.RouteNetworkElements;
            else
                routeNetworkElements = new LookupCollection<RouteNetworkElement>();
        }


        public static void GetInterestsAndRouteNetworkElementsByInterestIds(IQueryDispatcher queryDispatcher, InterestIdList interestIds, out LookupCollection<RouteNetworkInterest> interests, out LookupCollection<RouteNetworkElement> routeNetworkElements)
        {
            // Get interest information from existing span equipment
            var interestQueryResult = queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
                new GetRouteNetworkDetails(interestIds)
            ).Result;

            if (interestQueryResult.IsFailed)
                throw new ApplicationException($"Got an unexpected error querying route network element by interest ids: {interestQueryResult.Errors.First().Message}");

            if (interestQueryResult.Value.Interests != null)
                interests = interestQueryResult.Value.Interests;
            else
                interests = new LookupCollection<RouteNetworkInterest>();

            if (interestQueryResult.Value.RouteNetworkElements != null)
                routeNetworkElements = interestQueryResult.Value.RouteNetworkElements;
            else
                routeNetworkElements = new LookupCollection<RouteNetworkElement>();
        }

    }
}

