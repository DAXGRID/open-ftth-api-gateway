using GraphQL.Types;

namespace OpenFTTH.APIGateway.GraphQL.Installation.Types;

/// <summary>
/// Used for returning installation search result
/// </summary>
public sealed class InstallationSearchResponseType : ObjectGraphType<InstallationSearchResponse>
{
    public InstallationSearchResponseType()
    {
        Description = "Installation search result";

        Field("installationId", x => x.InstallationId, type: typeof(StringGraphType))
            .Description("Unique installation name maintained by the customer system.");

        Field("displayAddress", x => x.DisplayAddress, type: typeof(StringGraphType))
              .Description("Installation address");

        Field("additionalAddressInformation", x => x.AdditionalAddressInformation, type: typeof(StringGraphType))
               .Description("Installation additional address information");

        Field("distance", x => x.Distance, type: typeof(FloatGraphType))
            .Description("Distance from node");
    }
}
