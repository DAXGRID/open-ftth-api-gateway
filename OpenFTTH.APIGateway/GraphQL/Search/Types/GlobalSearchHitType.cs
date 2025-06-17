using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.Search;

namespace OpenFTTH.APIGateway.GraphQL.Search.Types
{
    public class GlobalSearchHitType : ObjectGraphType<GlobalSearchHit>
    {
        public GlobalSearchHitType(ILogger<GlobalSearchHitType> logger)
        {
            Field(x => x.Id, type: typeof(IdGraphType)).Description("Id");
            Field(x => x.ObjectType, type: typeof(IdGraphType)).Description("Type of object");
            Field(x => x.Label, type: typeof(IdGraphType)).Description("Label");
            Field(x => x.Xwgs, type: typeof(FloatGraphType)).Description("X coordinate in WGS84");
            Field(x => x.Ywgs, type: typeof(FloatGraphType)).Description("Y coordinate in WGS84");
            Field(x => x.Xetrs, type: typeof(FloatGraphType)).Description("X coordinate in ETRS89 UTM32");
            Field(x => x.Yetrs, type: typeof(FloatGraphType)).Description("Y coordinate in ETRS89 UTM32");
        }
    }


}

