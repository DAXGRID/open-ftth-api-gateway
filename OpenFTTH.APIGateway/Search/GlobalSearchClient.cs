using FluentResults;
using OpenFTTH.APIGateway.Util;
using OpenFTTH.CQRS;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using System;
using System.Collections.Generic;
using System.Globalization;
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
            List<GlobalSearchHit> searchResult = new();

            var nodeSearchResultTask = SearchForNodes(searchString, maxHits).ConfigureAwait(false);
            var addressSearchResultTask = SearchForAddresses(searchString, maxHits).ConfigureAwait(false);

            var nodeSearchResult = await nodeSearchResultTask;
            var addressSearchResult = await addressSearchResultTask;

            var halfMaxHits = (maxHits / 2);
            if (nodeSearchResult.Count > halfMaxHits)
            {
                // Add half max hit of node hits to the final search result
                for (int i = 0; i < halfMaxHits; i++)
                    searchResult.Add(nodeSearchResult[i]);
            }
            else
            {
                // Add all node hits to the final search result
                foreach (var nodeSearchHit in nodeSearchResult)
                    searchResult.Add(nodeSearchHit);
            }

            // Top up with address hits
            foreach (var addressSearchHit in addressSearchResult)
            {
                if (searchResult.Count < maxHits)
                    searchResult.Add(addressSearchHit);
                else
                    break;
            }

            return searchResult;
        }

        private async Task<List<GlobalSearchHit>> SearchForAddresses(string searchString, int maxHits)
        {
            var query = new SearchParameters
            {
                Text = searchString,
                QueryBy = "roadNameHouseNumber,postDistrictCode,postDistrictName,townName",
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

                var globalHit = new GlobalSearchHit(hit.Document.Id, "accessAddress", GetAddressLabel(hit.Document), wgs84Coord[0], wgs84Coord[1], xEtrs, yEtrs);

                result.Add(globalHit);
            }

            return result;
        }

        private async Task<List<GlobalSearchHit>> SearchForNodes(string searchString, int maxHits)
        {
            var query = new SearchParameters
            {
                Text = searchString,
                QueryBy = "name",
                PerPage = maxHits.ToString(),
                LimitHits = maxHits.ToString(),
                NumberOfTypos = "0"
            };

            var searchResult = await _typesenseClient.Search<RouteNodeSearchHit>("RouteNodes", query).ConfigureAwait(false);
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

                    var globalHit = new GlobalSearchHit(hit.Document.Id, "routeNode", hit.Document.Name, wgs84Coord[0], wgs84Coord[1], etrsCoord[0], etrsCoord[1]);
                    result.Add(globalHit);
                }
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
        public Guid Id { get; init; }
        public string Name { get; init; }
    }
}
