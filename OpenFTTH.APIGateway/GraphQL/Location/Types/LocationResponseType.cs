using System;
using GraphQL.Types;
using OpenFTTH.APIGateway.GraphQL.Core.Types;

namespace OpenFTTH.APIGateway.GraphQL.Location.Types;

/// <summary>
/// Used for URL parameter search queries from the OpenFTTH frontend.
/// </summary>
public sealed class LocationResponseType : ObjectGraphType<LocationResponse>
{
    public LocationResponseType()
    {
        Description = "Location";

        Field("envelope", x => x.Envelope, type: typeof(EnvelopeType))
            .Description("The extent of the location.");

        Field("routeElementId", x => x.RouteElementId, type: typeof(IdGraphType))
            .Description("The route element id.");

        Field("coordinate", x => x.Coordinate, type: typeof(PointType))
            .Description("The coordinate for the point.");
    }
}
