using FluentResults;
using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.GraphQL.Addresses.Types;
using OpenFTTH.APIGateway.Util;
using OpenFTTH.Address.API.Model;
using OpenFTTH.Address.API.Queries;
using OpenFTTH.CQRS;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenFTTH.APIGateway.GraphQL.Addresses.Queries
{
    public class AddressServiceQueries : ObjectGraphType
    {
        private readonly ILogger<AddressServiceQueries> _logger;

        public AddressServiceQueries(ILogger<AddressServiceQueries> logger, IQueryDispatcher queryDispatcher)
        {
            _logger = logger;

            Description = "GraphQL API for querying address information";

            Field<ListGraphType<NearestAddressSearchHitType>>(
                "nearestAccessAddresses",
                arguments:
                new QueryArguments(
                    new QueryArgument<IdGraphType> { Name = "routeNodeId" },
                    new QueryArgument<IdGraphType> { Name = "spanEquipmentOrSegmentId" },
                    new QueryArgument<FloatGraphType> { Name = "x" },
                    new QueryArgument<FloatGraphType> { Name = "y" },
                    new QueryArgument<IntGraphType> { Name = "srid" },
                    new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "maxHits" }
                ),
                resolve: context =>
                {
                    double x = context.GetArgument<double>("x");
                    double y = context.GetArgument<double>("y");
                    int srid = context.GetArgument<int>("srid");
                    int maxHits = context.GetArgument<int>("maxHits");
                    Guid routeNodeId = context.GetArgument<Guid>("routeNodeId");
                    Guid spanEquipmentOrSegmentId = context.GetArgument<Guid>("spanEquipmentOrSegmentId");

                    if (routeNodeId != Guid.Empty)
                    {
                        var nodeCoord = GetNodeCoordinates(routeNodeId, queryDispatcher);

                        if (nodeCoord == (0, 0))
                        {
                            context.Errors.Add(new ExecutionError($"Could not find a route node with id: {routeNodeId}"));
                            return null;
                        }

                        var getAddressInfoQuery = new GetAddressInfo(nodeCoord.Item1, nodeCoord.Item2, 25832, maxHits);

                        var result = queryDispatcher.HandleAsync<GetAddressInfo, Result<GetAddressInfoResult>>(getAddressInfoQuery).Result;

                        if (result.IsFailed)
                        {
                            context.Errors.Add(new ExecutionError(result.Errors.First().Message));
                            return null;
                        }

                        return MapToGraphQLAddressHits(result.Value);
                    }
                    else if (spanEquipmentOrSegmentId != Guid.Empty)
                    {
                        var segmentEnd = GetSpanSegmentEndCoordinate(spanEquipmentOrSegmentId, queryDispatcher);

                        // Find address near the from span equipment end
                        var getAddressInfoQuery = new GetAddressInfo(segmentEnd.Item1, segmentEnd.Item2, 25832, maxHits);

                        var getAddressInfoQueryResult = queryDispatcher.HandleAsync<GetAddressInfo, Result<GetAddressInfoResult>>(getAddressInfoQuery).Result;

                        if (getAddressInfoQueryResult.IsFailed)
                        {
                            context.Errors.Add(new ExecutionError(getAddressInfoQueryResult.Errors.First().Message));
                            return null;
                        }

                        return MapToGraphQLAddressHits(getAddressInfoQueryResult.Value);
                    }
                    else
                    {
                        var getAddressInfoQuery = new GetAddressInfo(x, y, srid, maxHits);

                        var result = queryDispatcher.HandleAsync<GetAddressInfo, Result<GetAddressInfoResult>>(getAddressInfoQuery).Result;

                        if (result.IsFailed)
                        {
                            context.Errors.Add(new ExecutionError(result.Errors.First().Message));
                            return null;
                        }

                        return MapToGraphQLAddressHits(result.Value);
                    }


                }
           );
        }

        private (double, double) GetNodeCoordinates(Guid nodeId, IQueryDispatcher queryDispatcher)
        {

            var routeNodeQueryResult = queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
                  new GetRouteNetworkDetails(new OpenFTTH.RouteNetwork.API.Model.RouteNetworkElementIdList() { nodeId })
                  {
                      RouteNetworkElementFilter = new RouteNetworkElementFilterOptions() { IncludeCoordinates = true }
                  }
              ).Result;

            if (routeNodeQueryResult.IsSuccess)
            {
                var etrsCoord = ConvertPointGeojsonToCoordArray(routeNodeQueryResult.Value.RouteNetworkElements.First().Coordinates);

                return (etrsCoord[0], etrsCoord[1]);
            }
            else
                return (0, 0);
        }

        private (double, double) GetSpanSegmentEndCoordinate(Guid spanSegmentId, IQueryDispatcher queryDispatcher)
        {
            // Query span equipment
            var equipmentQueryResult = queryDispatcher.HandleAsync<GetEquipmentDetails, FluentResults.Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new EquipmentIdList() { spanSegmentId })
                {
                    EquipmentDetailsFilter = new EquipmentDetailsFilterOptions { IncludeRouteNetworkTrace = true }
                }
            ).Result;

            if (equipmentQueryResult.IsFailed)
            {
                throw new ApplicationException($"Error querying span equipment by id: {spanSegmentId} " + equipmentQueryResult.Errors.First().Message);
            }

            if (equipmentQueryResult.Value.SpanEquipment == null || equipmentQueryResult.Value.SpanEquipment.Count == 0)
            {
                throw new ApplicationException($"Cannot find any span equipment containing a span segment with id: {spanSegmentId}");
            }

            Guid spanEquipmentInterestId = equipmentQueryResult.Value.SpanEquipment.First().WalkOfInterestId;

            // Query route network
            var routeNetworkQueryResult = queryDispatcher.HandleAsync<GetRouteNetworkDetails, FluentResults.Result<GetRouteNetworkDetailsResult>>(
                      new GetRouteNetworkDetails(new InterestIdList() { spanEquipmentInterestId })
                      {
                          RouteNetworkElementFilter = new RouteNetworkElementFilterOptions()
                          {
                              IncludeCoordinates = true,
                          },
                          RelatedInterestFilter = RelatedInterestFilterOptions.None
                      }
                  ).Result;

            if (routeNetworkQueryResult.IsFailed)
            {
                throw new ApplicationException($"Got error querying interest information for span equipment: {spanSegmentId} ERROR: " + routeNetworkQueryResult.Errors.First().Message);
            }

            if (routeNetworkQueryResult.Value.Interests == null || !routeNetworkQueryResult.Value.Interests.ContainsKey(spanEquipmentInterestId))
                throw new ApplicationException($"Got no info querying interest information for span equipment: {spanSegmentId}");

            var routeNetworkElementIds = routeNetworkQueryResult.Value.Interests[spanEquipmentInterestId].RouteNetworkElementRefs;

            var lastNodeInSpanSegment = routeNetworkElementIds.Last();

            var coord = GetNodeCoordinates(lastNodeInSpanSegment, queryDispatcher);

            _logger.LogInformation($"Address search info: Get coordinate of span segment: {spanSegmentId} in span equipment: {equipmentQueryResult.Value.SpanEquipment.First().Name} {equipmentQueryResult.Value.SpanEquipment.First().Id} Route node id: {lastNodeInSpanSegment} successfully returned: x={coord.Item1} y={coord.Item2}");


            return coord;
        }

        private double[] ConvertPointGeojsonToCoordArray(string geojson)
        {
            List<double> result = new();

            var geojsonSplit = geojson.Replace("[", "").Replace("]", "").Split(',');

            foreach (var coord in geojsonSplit)
            {
                result.Add(Double.Parse(coord, CultureInfo.InvariantCulture));
            }

            if (result.Count != 2)
                throw new ApplicationException($"Expected point geojson, but got: '{geojson}'");

            return result.ToArray();
        }



        private List<NearestAddressSearchHit> MapToGraphQLAddressHits(GetAddressInfoResult addressQueryResult)
        {
            List<NearestAddressSearchHit> result = new();

            foreach (var addressHit in addressQueryResult.AddressHits)
            {
                result.Add(new NearestAddressSearchHit()
                {
                    AccessAddress = MapAccessAddress(addressHit.Key, addressQueryResult),
                    Distance = addressHit.Distance.Value
                });
            }

            return result;
        }

        public static AccessAddressData MapAccessAddress(Guid accessAddressId, GetAddressInfoResult addressQueryResult)
        {
            var searchAccessAddress = addressQueryResult.AccessAddresses[accessAddressId];

            var wgs84Coord = UTM32WGS84Converter.ConvertFromUTM32NToWGS84(searchAccessAddress.AddressPoint.X, searchAccessAddress.AddressPoint.Y);

            List<UnitAddress> unitAddresses = new();

            foreach (var unitAddress in addressQueryResult.UnitAddresses)
            {
                if (unitAddress.AccessAddressId == searchAccessAddress.Id)
                    unitAddresses.Add(unitAddress);
            }

            var mappedAccessAddress = new AccessAddressData()
            {
                Id = searchAccessAddress.Id,
                ExternalId = searchAccessAddress.ExternalId,
                HouseNumber = searchAccessAddress.HouseNumber,
                PostDistrictCode = searchAccessAddress.PostDistrictCode,
                PostDistrict = searchAccessAddress.PostDistrict,
                RoadCode = searchAccessAddress.RoadCode,
                RoadName = searchAccessAddress.RoadName,
                TownName = searchAccessAddress.TownName,
                MunicipalCode = searchAccessAddress.MunicipalCode,
                Xetrs = searchAccessAddress.AddressPoint.X,
                Yetrs = searchAccessAddress.AddressPoint.Y,
                Xwgs = wgs84Coord[0],
                Ywgs = wgs84Coord[1],
                UnitAddresses = unitAddresses
            };

            return mappedAccessAddress;
        }
    }
}
