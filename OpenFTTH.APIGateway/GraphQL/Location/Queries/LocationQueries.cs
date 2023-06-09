using System;
using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using OpenFTTH.CQRS;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.GraphQL.Location.Types;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using OpenFTTH.Address.API.Model;
using OpenFTTH.Address.API.Queries;
using FluentResults;
using System.Linq;
using OpenFTTH.APIGateway.Util;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.RouteNetwork.API.Model;
using System.Globalization;

namespace OpenFTTH.APIGateway.GraphQL.Location.Queries;

public class LocationQueries : ObjectGraphType
{
    public LocationQueries(IQueryDispatcher queryDispatcher)
    {
        Description = "GraphQL API for location search.";

        Field<LocationResponseType>("lookupLocation")
            .Description("Lookup location.")
            .Arguments(
                new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "kind"
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "value"
                    })
            ).ResolveAsync(async context =>
            {
                const double EXPAND_ENVELOPE_METER = 100;
                var converter = new UTM32WGS84Converter();

                var kind = context.GetArgument<string>("kind");
                var value = context.GetArgument<string>("value");

                if (kind.Equals("InstallationId", StringComparison.OrdinalIgnoreCase))
                {
                    var equipmentQueryResult = await queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                        new GetEquipmentDetails(value));

                    if (equipmentQueryResult.IsFailed)
                    {
                        context.Errors.Add(new ExecutionError(equipmentQueryResult.Errors.First().Message));
                        return null;
                    }

                    if (equipmentQueryResult.Value.TerminalEquipment.Count == 0)
                    {
                        context.Errors.Add(new ExecutionError($"Could not find any installation on id '{value}'."));
                        return null;
                    }

                    var nodeContainerId = equipmentQueryResult.Value.TerminalEquipment.First().NodeContainerId;
                    var nodeEquipmentResult = await queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                        new GetEquipmentDetails(
                            new EquipmentIdList(
                                new Guid[] { nodeContainerId }
                            )
                        )
                    );

                    var routeNodeId = nodeEquipmentResult.Value.NodeContainers.First().RouteNodeId;

                    var routeNodeQueryResult = await queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
                        new GetRouteNetworkDetails(new RouteNetworkElementIdList() { routeNodeId })
                        {
                            RouteNetworkElementFilter = new RouteNetworkElementFilterOptions() { IncludeCoordinates = true }
                        }
                    );

                    if (routeNodeQueryResult.IsFailed)
                    {
                        context.Errors.Add(new ExecutionError(routeNodeQueryResult.Errors.First().Message));
                        return null;
                    }

                    if (routeNodeQueryResult.Value.RouteNetworkElements.Count == 0)
                    {
                        context.Errors.Add(new ExecutionError($"Could not find any route node with id '{routeNodeId}' doing installation lookup."));
                        return null;
                    }

                    var installationAddressPoint = ConvertPointGeojsonToPoint(routeNodeQueryResult.Value.RouteNetworkElements.First().Coordinates);
                    var envelopeETRS89 = new Envelope(installationAddressPoint.Coordinates);

                    envelopeETRS89.ExpandBy(EXPAND_ENVELOPE_METER);

                    var installationAddressPointWGS84 = PointToWGS84(installationAddressPoint);
                    var envelopeWGS84 = EnvelopeToWGS84(envelopeETRS89);

                    return new LocationResponse(
                        envelopeWGS84,
                        routeNodeId,
                        installationAddressPointWGS84
                    );
                }
                else if (kind.Equals("UnitAddressId", StringComparison.OrdinalIgnoreCase))
                {
                    var unitAddressId = Guid.Parse(value);
                    var getAddressInfoQuery = new GetAddressInfo(new Guid[] { unitAddressId });
                    var result = await queryDispatcher
                        .HandleAsync<GetAddressInfo, Result<GetAddressInfoResult>>(getAddressInfoQuery);

                    if (result.IsFailed)
                    {
                        context.Errors.Add(new ExecutionError(result.Errors.First().Message));
                        return null;
                    }

                    if (result.Value.UnitAddresses.Count == 0)
                    {
                        context.Errors.Add(
                            new ExecutionError(
                                $"Could not find any unit addresses with id '{unitAddressId}'"));

                        return null;
                    }

                    var accessAddress = result.Value.AccessAddresses[result.Value.UnitAddresses.First().AccessAddressId];
                    var envelopeETRS89 = new Envelope(accessAddress.AddressPoint.Coordinates);
                    envelopeETRS89.ExpandBy(EXPAND_ENVELOPE_METER);

                    var unitAddressPointWGS84 = PointToWGS84(accessAddress.AddressPoint);
                    var envelopeWGS84 = EnvelopeToWGS84(envelopeETRS89);

                    return new LocationResponse(
                        envelopeWGS84,
                        null, // We do not have a route element id for unit address id lookup, so we return null.
                        unitAddressPointWGS84);
                }
                else
                {
                    context.Errors.Add(new ExecutionError($"Could not handle type kind '{kind}'"));
                    return null;
                }
            });
    }

    private static Point PointToWGS84(Point point)
    {
        var converter = new UTM32WGS84Converter();
        var wgs84Coordinates = converter.ConvertFromUTM32NToWGS84(point.X, point.Y);
        return new Point(wgs84Coordinates[0], wgs84Coordinates[1]);
    }

    private static Envelope EnvelopeToWGS84(Envelope envelope)
    {
        var converter = new UTM32WGS84Converter();
        return converter.ConvertFromUTM32NToWGS84(envelope);
    }

    private static Point ConvertPointGeojsonToPoint(string geojson)
    {
        var geojsonSplit = geojson.Replace("[", "").Replace("]", "").Split(',');
        if (geojsonSplit.Length != 2)
            throw new ApplicationException($"Expected point geojson, but got: '{geojson}'");

        var result = new double[2];
        result[0] = Double.Parse(geojsonSplit[0], CultureInfo.InvariantCulture);
        result[1] = Double.Parse(geojsonSplit[1], CultureInfo.InvariantCulture);

        return new Point(result[0], result[1]);
    }
}
