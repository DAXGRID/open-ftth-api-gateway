using System;
using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.GraphQL.Location.Types;
using NetTopologySuite.Geometries;

namespace OpenFTTH.APIGateway.GraphQL.Location.Queries;

public class LocationQueries : ObjectGraphType
{
    public LocationQueries()
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
            ).Resolve(context =>
            {
                var kind = context.GetArgument<string>("kind");
                var value = context.GetArgument<string>("value");

                // TODO handle cases here
                return new LocationResponse(
                    new Envelope(10, 20, 30, 40),
                    Guid.NewGuid(),
                    new Point(10.0, 20.0)
                );
            });
    }
}
