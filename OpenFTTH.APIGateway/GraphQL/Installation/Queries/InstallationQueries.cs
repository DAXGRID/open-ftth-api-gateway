using GraphQL;
using GraphQL.Types;
using OpenFTTH.APIGateway.GraphQL.Addresses.Types;
using OpenFTTH.APIGateway.GraphQL.Installation.Types;
using OpenFTTH.APIGateway.GraphQL.Location.Types;
using OpenFTTH.CQRS;
using System;
using System.Collections.Generic;

namespace OpenFTTH.APIGateway.GraphQL.Installation.Queries;

public class InstallationQueries : ObjectGraphType
{
    public InstallationQueries(IQueryDispatcher queryDispatcher)
    {
        Description = "GraphQL API for querying installation information";

        Field<ListGraphType<InstallationSearchResponseType>>("nearestUndocumentedInstallations")
                .Arguments(new QueryArguments(
                    new QueryArgument<IdGraphType> { Name = "routeNodeId" },
                    new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "maxHits" },
                    new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "searchRadiusMeter" }
                ))
                .ResolveAsync(async (context) =>
                {
                    Guid routeNodeId = context.GetArgument<Guid>("routeNodeId");
                    int maxHits = context.GetArgument<int>("maxHits");

                    List<InstallationSearchResponse> result = new List<InstallationSearchResponse>();

                    // TODO: Lookup via installation projection
                    result.Add(new InstallationSearchResponse("12345678", "Engumvej 3", "Skur", 2.34));
                    result.Add(new InstallationSearchResponse("23233678", "Engumvej 3", null, 2));
                    result.Add(new InstallationSearchResponse("32343444", "Engumvej 6", null, 7));
                    result.Add(new InstallationSearchResponse("34344344", "Engumvej 5", null, 13.7));

                    return result;
                });
    }
}
