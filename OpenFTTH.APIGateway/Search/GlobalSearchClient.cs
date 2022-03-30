using FluentResults;
using Newtonsoft.Json;
using OpenFTTH.APIGateway.Util;
using OpenFTTH.CQRS;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Typesense;

namespace OpenFTTH.APIGateway.Search
{
    public class GlobalSearchClient
    {
        private readonly ITypesenseClient _typesenseClient;
        private readonly IQueryDispatcher _queryDispatcher;

        public GlobalSearchClient(ITypesenseClient typesenseClient, IQueryDispatcher queryDispatcher)
        {
            _typesenseClient = typesenseClient;
            _queryDispatcher = queryDispatcher;
        }

        public async Task<List<GlobalSearchHit>> Search(string searchString, int maxHits)
        {
            var nodeSearchResultTask = SearchForNodes(searchString, maxHits);
            //var equipmentsSearchResultTask = SearchForEquipments(searchString, maxHits);
            var addressSearchResultTask = SearchForAddresses(searchString, maxHits);

            var nodeSearchResult = await nodeSearchResultTask;
            //var equipmentsSearchResult = await equipmentsSearchResultTask;
            var addressSearchResult = await addressSearchResultTask;

            List<GlobalSearchHit> searches = new();
            searches.AddRange(nodeSearchResult);
            //searches.AddRange(equipmentsSearchResult);

            var result = searches
                .OrderByDescending(x => x.TextMatch)
                .ToList();

            // We put addreses on last because it's multi-field search and therefore has very high text match score.
            if (result.Count() < maxHits)
                result.AddRange(addressSearchResult.OrderByDescending(x => x.TextMatch));

            return result.Take(maxHits).ToList();
        }

        private async Task<List<GlobalSearchHit>> SearchForAddresses(string searchString, int maxHits)
        {
            var query = new SearchParameters(searchString, "roadNameHouseNumber,postDistrictCode,postDistrictName,townName")
            {
                PerPage = maxHits.ToString(),
                LimitHits = maxHits.ToString(),
                QueryByWeights = "5,3,3,2"
            };

            var searchResult = await _typesenseClient.Search<OfficialAccessAddressSearchHit>("Addresses", query).ConfigureAwait(false);

            List<GlobalSearchHit> result = new();

            foreach (var hit in searchResult.Hits)
            {
                var xEtrs = Double.Parse(hit.Document.EastCoordinate, CultureInfo.InvariantCulture);
                var yEtrs = Double.Parse(hit.Document.NorthCoordinate, CultureInfo.InvariantCulture);

                var wgs84Coord = UTM32WGS84Converter.ConvertFromUTM32NToWGS84(xEtrs, yEtrs);

                var globalHit = new GlobalSearchHit(hit.Document.Id, "accessAddress", GetAddressLabel(hit.Document), wgs84Coord[0], wgs84Coord[1], xEtrs, yEtrs, hit.TextMatch);

                result.Add(globalHit);
            }

            return result;
        }

        private async Task<List<GlobalSearchHit>> SearchForNodes(string searchString, int maxHits)
        {
            var query = new SearchParameters(searchString, "name")
            {
                PerPage = maxHits.ToString(),
                LimitHits = maxHits.ToString(),
                NumberOfTypos = "0"
            };

            var searchResult = await _typesenseClient.Search<RouteNodeSearchHit>("RouteNodes", query).ConfigureAwait(false);
            if (searchResult.Hits.Count == 0)
                return new();

            RouteNetworkElementIdList routeNodeIds = new();
            foreach (var hit in searchResult.Hits)
            {
                routeNodeIds.Add(hit.Document.Id);
            }

            var routeNodeQueryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
                new GetRouteNetworkDetails(routeNodeIds)
                {
                    RouteNetworkElementFilter = new RouteNetworkElementFilterOptions() { IncludeCoordinates = true }
                }
            ).ConfigureAwait(false);

            List<GlobalSearchHit> result = new();
            if (routeNodeQueryResult.IsSuccess)
            {
                foreach (var hit in searchResult.Hits)
                {
                    var etrsCoord = ConvertPointGeojsonToCoordArray(routeNodeQueryResult.Value.RouteNetworkElements[hit.Document.Id].Coordinates);
                    var wgs84Coord = UTM32WGS84Converter.ConvertFromUTM32NToWGS84(etrsCoord[0], etrsCoord[1]);

                    var globalHit = new GlobalSearchHit(
                        hit.Document.Id,
                        "routeNode",
                        hit.Document.Name,
                        wgs84Coord[0],
                        wgs84Coord[1],
                        etrsCoord[0],
                        etrsCoord[1],
                        hit.TextMatch);

                    result.Add(globalHit);
                }
            }

            return result;
        }

        private async Task<List<GlobalSearchHit>> SearchForEquipments(string searchString, int maxHits)
        {
            var query = new SearchParameters(searchString, "name")
            {
                PerPage = maxHits.ToString(),
                LimitHits = maxHits.ToString(),
                NumberOfTypos = "0"
            };

            var searchResult = await _typesenseClient.Search<EquipmentSearchHit>("equipments", query).ConfigureAwait(false);
            if (searchResult.Hits.Count == 0)
                return new();

            EquipmentIdList equipmentIdList = new();
            foreach (var hit in searchResult.Hits)
            {
                equipmentIdList.Add(hit.Document.Id);
            }

            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
               new GetEquipmentDetails(equipmentIdList)
            );

            Result<GetEquipmentDetailsResult> nodeEquipmentResult = null;
            if (equipmentQueryResult.IsSuccess)
            {
                EquipmentIdList nodeContainerIds = new();
                foreach (var equipment in equipmentQueryResult.Value.TerminalEquipment)
                {
                    nodeContainerIds.Add(equipment.NodeContainerId);
                }

                nodeEquipmentResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                  new GetEquipmentDetails(nodeContainerIds)
                ).ConfigureAwait(false);
            }
            else
            {
                throw new Exception($"{nameof(equipmentQueryResult)} failed.");
            }

            RouteNetworkElementIdList routeNodeIds = new();
            if (nodeEquipmentResult is not null && nodeEquipmentResult.IsSuccess)
            {
                foreach (var nodeEquipment in nodeEquipmentResult.Value.NodeContainers)
                {
                    routeNodeIds.Add(nodeEquipment.RouteNodeId);
                }
            }
            else
            {
                throw new Exception($"{nameof(nodeEquipmentResult)} failed.");
            }

            var routeNodeQueryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
                new GetRouteNetworkDetails(routeNodeIds)
                {
                    RouteNetworkElementFilter = new RouteNetworkElementFilterOptions() { IncludeCoordinates = true }
                }
            ).ConfigureAwait(false);

            List<GlobalSearchHit> result = new();
            if (routeNodeQueryResult.IsSuccess)
            {
                foreach (var hit in searchResult.Hits)
                {
                    var nodeContainerId = equipmentQueryResult.Value.TerminalEquipment[hit.Document.Id].NodeContainerId;
                    var routeNodeId = nodeEquipmentResult.Value.NodeContainers[nodeContainerId].RouteNodeId;
                    var routeNode = routeNodeQueryResult.Value.RouteNetworkElements[routeNodeId];
                    var etrsCoord = ConvertPointGeojsonToCoordArray(routeNode.Coordinates);
                    var wgs84Coord = UTM32WGS84Converter.ConvertFromUTM32NToWGS84(etrsCoord[0], etrsCoord[1]);

                    var globalHit = new GlobalSearchHit(
                        routeNodeId,
                        "routeNode",
                        hit.Document.Name,
                        wgs84Coord[0],
                        wgs84Coord[1],
                        etrsCoord[0],
                        etrsCoord[1],
                        hit.TextMatch);

                    result.Add(globalHit);
                }
            }
            else
            {
                throw new Exception($"{nameof(routeNodeQueryResult)} failed.");
            }

            return result;
        }

        private static double[] ConvertPointGeojsonToCoordArray(string geojson)
        {
            var geojsonSplit = geojson.Replace("[", "").Replace("]", "").Split(',');
            if (geojsonSplit.Length != 2)
                throw new ApplicationException($"Expected point geojson, but got: '{geojson}'");

            var result = new double[2];
            result[0] = Double.Parse(geojsonSplit[0], CultureInfo.InvariantCulture);
            result[1] = Double.Parse(geojsonSplit[1], CultureInfo.InvariantCulture);

            return result;
        }

        private static string GetAddressLabel(OfficialAccessAddressSearchHit address)
        {
            var result = address.RoadNameHouseNumber + ", ";

            if (address.TownName != null)
                result += address.TownName + ", ";

            result += address.PostDistrictCode + " " + address.PostDistrictName;

            return result;
        }
    }

    public record OfficialAccessAddressSearchHit
    {
        public Guid Id { get; init; }
        public string Status { get; init; }
        public string RoadNameHouseNumber { get; init; }
        public string PostDistrictCode { get; init; }
        public string PostDistrictName { get; init; }
        public string EastCoordinate { get; init; }
        public string NorthCoordinate { get; init; }
        public string TownName { get; init; }
    }

    public record RouteNodeSearchHit
    {
        [JsonProperty("Id")]
        public Guid Id { get; init; }
        [JsonProperty("Name")]
        public string Name { get; init; }
    }

    public record EquipmentSearchHit
    {
        [JsonProperty("id")]
        public Guid Id { get; init; }
        [JsonProperty("name")]
        public string Name { get; init; }
    }
}
