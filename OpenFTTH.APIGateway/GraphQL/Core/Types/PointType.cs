using GraphQL.Types;
using NetTopologySuite.Geometries;

namespace OpenFTTH.APIGateway.GraphQL.Core.Types
{
    public class PointType : ObjectGraphType<Point>
    {
        public PointType()
        {
            Field(x => x.X, type: typeof(FloatGraphType))
                .Name("x")
                .Description("The x coordinate.");

            Field(x => x.Y, type: typeof(FloatGraphType))
                .Name("y")
                .Description("The y coordinate.");
        }
    }
}
