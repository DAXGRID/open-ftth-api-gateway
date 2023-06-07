using System;
using NetTopologySuite.Geometries;

namespace OpenFTTH.APIGateway.GraphQL.Location.Types;

public sealed record LocationResponse
{
    public Envelope Envelope { get; init; }
    public Guid? RouteElementId { get; init; }
    public Point Coordinate { get; init; }

    public LocationResponse(
        Envelope envelope,
        Guid? routeElementId,
        Point coordinate)
    {
        Envelope = envelope;
        RouteElementId = routeElementId;
        Coordinate = coordinate;
    }
}
