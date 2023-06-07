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
                const int EXPAND_ENVELOPE = 200;
                var converter = new UTM32WGS84Converter();

                var kind = context.GetArgument<string>("kind").ToUpperInvariant();
                var value = context.GetArgument<string>("value");

                if (kind == "InstallationId".ToUpperInvariant())
                {
                    return new LocationResponse(
                        new Envelope(
                            9.841922420538234,
                            9.846643823320909,
                            55.84230941549938,
                            55.83975657387827),
                        Guid.Parse("7e2da86a-179f-4d16-8507-30037a672fb8"),
                        new Point(9.84454407055106, 55.84098217939197)
                    );
                }
                else if (kind == "UnitAddressId".ToUpperInvariant())
                {
                    var unitAddressId = Guid.Parse(value);
                    var getAddressInfoQuery = new GetAddressInfo(new Guid[] { unitAddressId });
                    var result = await queryDispatcher.HandleAsync<GetAddressInfo, Result<GetAddressInfoResult>>(getAddressInfoQuery);
                    if (result.IsFailed)
                    {
                        context.Errors.Add(new ExecutionError(result.Errors.First().Message));
                        return null;
                    }

                    if (result.Value.UnitAddresses.Count == 0)
                    {
                        context.Errors.Add($"Could not find any unit addresses with id '{unitAddressId}'");
                        return null;
                    }

                    var accessAddress = result.Value.AccessAddresses[result.Value.UnitAddresses.First().AccessAddressId];
                    var wgs84Coordinates = converter.ConvertFromUTM32NToWGS84(accessAddress.AddressPoint.X, accessAddress.AddressPoint.Y);
                    var accessAddressPointWGS84 = new Point(accessAddressPointWGS84[0], accessAddressPointWGS84[1]);

                    return new LocationResponse(
                        accessAddressPoint.EnvelopeInternal.ExpandBy(EXPAND_ENVELOPE),
                        null, // We do not have a route element id for unit address id lookup, so we return null.
                        accessAddressPointWGS84);
                }
                else
                {
                    context.Errors.Add($"Could not handle type kind '{kind}'");
                    return null;
                }
            });
    }
}
