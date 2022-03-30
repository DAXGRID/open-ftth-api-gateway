using GraphQL.Types;
using OpenFTTH.Events.RouteNetwork.Infos;

namespace OpenFTTH.APIGateway.GraphQL.RouteNetwork.Types
{
    public class RouteSegmentKindEnumType : EnumerationGraphType<RouteSegmentKindEnum>
    {
        public RouteSegmentKindEnumType()
        {
            Name = "RouteSegmentKindEnum";
            Description = @"The type of structure - i.e. cabinet, hand hole etc.";
        }
    }
}
