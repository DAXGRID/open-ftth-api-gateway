namespace OpenFTTH.APIGateway.GraphQL.Addresses.Types
{
    public class NearestAddressSearchHit
    {
        public AccessAddressData AccessAddress { get; set; }
        public double Distance { get; set; }
    }
}
