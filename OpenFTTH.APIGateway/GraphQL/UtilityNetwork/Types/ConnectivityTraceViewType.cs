using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;
using System.Linq;

namespace OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types
{
    public class ConnectivityTraceViewType : ObjectGraphType<ConnectivityTraceView>
    {
        public ConnectivityTraceViewType(ILogger<ConnectivityTraceViewType> logger, UTM32WGS84Converter coordinateConverter)
        {
            Field(x => x.CircuitName, type: typeof(StringGraphType)).Description("Circuit name");
            Field(x => x.Hops, type: typeof(ListGraphType<ConnectivityTraceViewHopInfoType>)).Description("The trace hops");


            Field<CoordinateEnvelopeType>("Envelope")
               .Description("Envelope")
               .Resolve(context =>
               {
                   var coordinateConverterResult = coordinateConverter.ConvertGeoJsonLineStringsToWgs84(
                       context.Source.Hops.SelectMany(h => h.RouteSegmentGeometries).ToArray());

                   var envelope = new CoordinateEnvelope();

                   envelope.ETRS89MinX = coordinateConverterResult.ETRS89BoundingBox.MinX;
                   envelope.ETRS89MinY = coordinateConverterResult.ETRS89BoundingBox.MinY;
                   envelope.ETRS89MaxX = coordinateConverterResult.ETRS89BoundingBox.MaxX;
                   envelope.ETRS89MaxY = coordinateConverterResult.ETRS89BoundingBox.MaxY;
                   envelope.WGS84MinX = coordinateConverterResult.WGS84BoundingBox.MinX;
                   envelope.WGS84MinY = coordinateConverterResult.WGS84BoundingBox.MinY;
                   envelope.WGS84MaxX = coordinateConverterResult.WGS84BoundingBox.MaxX;
                   envelope.WGS84MaxY = coordinateConverterResult.WGS84BoundingBox.MaxY;

                   return envelope;
               });
        }
    }
}
