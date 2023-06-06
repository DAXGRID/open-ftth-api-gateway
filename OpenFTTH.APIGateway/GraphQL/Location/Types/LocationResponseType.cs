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

        Field(x => x.Envelope, type: typeof(EnvelopeType))
            .Name("envelope")
            .Description("The extent of the location.");

        Field(x => x.RouteElementId, type: typeof(EnvelopeType))
            .Name("routeElementId")
            .Description("The route element id.");

        Field(x => x.Coordinate, type: typeof(PointType))
            .Name("coordinate")
            .Description("The coordinate for the point.");
    }
}
