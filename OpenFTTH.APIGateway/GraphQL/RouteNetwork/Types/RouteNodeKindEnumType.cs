using GraphQL.Types;
using OpenFTTH.Events.RouteNetwork.Infos;

namespace OpenFTTH.APIGateway.GraphQL.RouteNetwork.Types
{
    public class RouteNodeKindEnumType : EnumerationGraphType<RouteNodeKindEnum>
    {
        public RouteNodeKindEnumType()
        {
            Name = "RouteNodeKindEnum";
            Description = @"The type of structure - i.e. cabinet, hand hole etc.";
        }
    }
}
