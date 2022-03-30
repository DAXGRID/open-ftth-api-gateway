using GraphQL.Types;
using OpenFTTH.Address.API.Model;

namespace OpenFTTH.APIGateway.GraphQL.Addresses.Types
{
    public class UnitAddressType : ObjectGraphType<UnitAddress>
    {
        public UnitAddressType()
        {
            Field(x => x.Id, type: typeof(IdGraphType)).Description("Internal Unit Address Id");
            Field(x => x.ExternalId, type: typeof(IdGraphType)).Description("External Unit Address Id");
            Field(x => x.FloorName, type: typeof(StringGraphType)).Description("FloorName");
            Field(x => x.SuitName, type: typeof(StringGraphType)).Description("SuitName");
        }
    }
}
