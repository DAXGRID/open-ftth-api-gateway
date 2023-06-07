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
                    new Envelope(
                        9.841922420538234,
                        9.846643823320909,
                        55.84230941549938,
                        55.83975657387827),
                    Guid.Parse("7e2da86a-179f-4d16-8507-30037a672fb8"),
                    new Point(9.84454407055106, 55.84098217939197)
                );
            });
    }
}
