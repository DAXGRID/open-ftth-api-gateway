using GraphQL.DataLoader;
using GraphQL.Types;

namespace OpenFTTH.APIGateway.CoreTypes
{
    public class GeometryType : ObjectGraphType<Geometry>
    {
        public GeometryType(IDataLoaderContextAccessor dataLoader)
        {
            Description = "A GeoJson Geometry";

            Field("type", x => x.GeoJsonType, type: typeof(IdGraphType)).Description("Type of the geometry");
            Field("coordinates", x => x.GeoJsonCoordinates, type: typeof(IdGraphType)).Description("Coordinates of the geometry");
        }
    }
}
