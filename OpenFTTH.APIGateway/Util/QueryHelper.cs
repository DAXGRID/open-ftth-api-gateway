using OpenFTTH.Results;
using OpenFTTH.CQRS;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using System;
using System.Linq;

namespace OpenFTTH.APIGateway.Util
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

        public static Result<NodeContainer> GetNodeContainer(IQueryDispatcher queryDispatcher, Guid nodeContainerId)
        {
            var equipmentIdList = new EquipmentIdList();
            equipmentIdList.Add(nodeContainerId);

            // Query all the equipments related to the route network element
            var equipmentQueryResult = queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                    new GetEquipmentDetails(equipmentIdList)
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


            return Result.Fail(new Error($"Failed to find node container with id: {nodeContainerId}"));
        }
    }
}
