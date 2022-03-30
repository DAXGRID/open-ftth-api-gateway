using GraphQL.Types;
using OpenFTTH.Events.Core.Infos;

namespace OpenFTTH.APIGateway.GraphQL.RouteNetwork.Types
{
    public class MappingMethodEnumType : EnumerationGraphType<MappingMethodEnum>
    {
        public MappingMethodEnumType()
        {
            Name = "MappingMethodEnum";
            Description = @"How the asset was digitized geographically.";
        }
    }
}
