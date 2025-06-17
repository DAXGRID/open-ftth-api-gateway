using GraphQL.DataLoader;
using GraphQL.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenFTTH.APIGateway.CoreTypes;
using OpenFTTH.APIGateway.DynamicProperties;
using OpenFTTH.APIGateway.GraphQL.DynamicProperties.Types;
using OpenFTTH.Schematic.API.Model.DiagramLayout;
using System.Collections.Generic;

namespace OpenFTTH.APIGateway.GraphQL.Schematic.Types
{
    public class DiagramObjectType : ObjectGraphType<DiagramObject>
    {
        public DiagramObjectType(IDataLoaderContextAccessor dataLoader)
        {
            Description = "Diagram object";

            Field(x => x.Style, type: typeof(StringGraphType)).Description("Style name");
            Field(x => x.Label, type: typeof(StringGraphType)).Description("Label");
            Field(x => x.DrawingOrder, type: typeof(IntGraphType)).Description("DrawingOrder");

            Field<GeometryType>("geometry")
              .Resolve(context =>
              {
                  return MapGeometry(context.Source.Geometry);
              });

            Field<IdGraphType>("refId")
              .Resolve(context =>
              {
                  if (context.Source.IdentifiedObject != null)
                      return context.Source.IdentifiedObject.RefId;
                  return null;
              });

            Field<StringGraphType>("refClass")
            .Resolve(context =>
            {
                if (context.Source.IdentifiedObject != null)
                    return context.Source.IdentifiedObject.RefClass;
                return null;
            });

            Field<ListGraphType<DynamicPropertyType>>("properties")
              .Description("eventually extra dynamic properties defined on this object")
              .Resolve(context =>
              {
                  if (context.Source.Properties != null)
                  {
                      List<DynamicProperty> dynamicProperties = new List<DynamicProperty>();

                      foreach (var property in context.Source.Properties)
                      {
                          dynamicProperties.Add(new DynamicProperty() { Name = property.Key, Value = property.Value });
                      }

                      return dynamicProperties;
                  }

                  return null;

              });

        }

        private Geometry MapGeometry(NetTopologySuite.Geometries.Geometry geometry)
        {
            var writer = new NetTopologySuite.IO.GeoJsonWriter();
            var geometryJson = writer.Write(geometry);

            var geometryObj = JObject.Parse(geometryJson);

            var coordinatesJson = geometryObj["coordinates"].ToString(Formatting.None);
            var typeJson = geometryObj["type"].ToString(Formatting.None).Replace("\"", "");

            return new Geometry(typeJson, coordinatesJson);
        }
    }
}
