using System;
using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.GraphQL.Location.Types;

namespace OpenFTTH.APIGateway.GraphQL.Location.Queries;

public class LocationQueries : ObjectGraphType<LocationResponseType>
{
    public LocationQueries()
    {
        Description = "GraphQL API for location search.";

        Field<LocationResponseType>("location")
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
                    }
                )
            ).Resolve(context =>
            {
                var kind = context.GetArgument<string>("kind");
                var value = context.GetArgument<string>("value");

                // TODO handle cases here
                return new LocationResponse();
            });

        // First retrieve the url parameters
    }
}
