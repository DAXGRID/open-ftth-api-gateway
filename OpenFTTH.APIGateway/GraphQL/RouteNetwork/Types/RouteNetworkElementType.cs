using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.RouteNetwork.API.Model;

namespace OpenFTTH.APIGateway.GraphQL.RouteNetwork.Types
{
    public class RouteNetworkElementType : ObjectGraphType<RouteNetworkElement>
    {
        public RouteNetworkElementType(ILogger<RouteNetworkElementType> logger)
        {
            Field(x => x.Id, type: typeof(IdGraphType)).Description("Guid property");
            Field(x => x.Kind, type: typeof(IdGraphType)).Description("Route Node or Segemtn");
            Field(x => x.RouteSegmentInfo, type: typeof(RouteSegmentInfoType)).Description("Route node specific properties");
            Field(x => x.RouteNodeInfo, type: typeof(RouteNodeInfoType)).Description("Route node specific properties");
            Field(x => x.NamingInfo, type: typeof(NamingInfoType)).Description("Asset info");
            Field(x => x.LifecycleInfo, type: typeof(LifecycleInfoType)).Description("Lifecycle info");
            Field(x => x.MappingInfo, type: typeof(MappingInfoType)).Description("Mapping/digitizing method info");
            Field(x => x.SafetyInfo, type: typeof(SafetyInfoType)).Description("Safety info");

            Field<BooleanGraphType>("hasRelatedEquipment")
              .Description("The specification used to create the span equipment")
              .Resolve(context =>
              {
                  return (context.Source.InterestRelations != null && context.Source.InterestRelations.Length > 0);
              });

        }
    }
}
