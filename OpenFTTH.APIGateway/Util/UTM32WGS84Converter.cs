﻿using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.APIGateway.Util
{
    public class UTM32WGS84Converter
    {
        private readonly CoordinateSystem _fromCS;
        private readonly CoordinateSystem _toCS;
        private readonly CoordinateTransformationFactory _ctfac;
        private readonly ICoordinateTransformation _trans;

        private static readonly double _boundingBoxExtraMarginPercent = 5;

        public UTM32WGS84Converter()
        {
            // ETRS 89 UTM 32
            string utmWkt = @"
                PROJCS[""ETRS89 / UTM zone 32N"",
                GEOGCS[""ETRS89"",
                    DATUM[""European_Terrestrial_Reference_System_1989"",
                        SPHEROID[""GRS 1980"",6378137,298.257222101,
                            AUTHORITY[""EPSG"",""7019""]],
                        AUTHORITY[""EPSG"",""6258""]],
                    PRIMEM[""Greenwich"",0,
                        AUTHORITY[""EPSG"",""8901""]],
                    UNIT[""degree"",0.01745329251994328,
                        AUTHORITY[""EPSG"",""9122""]],
                    AUTHORITY[""EPSG"",""4258""]],
                UNIT[""metre"",1,
                    AUTHORITY[""EPSG"",""9001""]],
                PROJECTION[""Transverse_Mercator""],
                PARAMETER[""latitude_of_origin"",0],
                PARAMETER[""central_meridian"",9],
                PARAMETER[""scale_factor"",0.9996],
                PARAMETER[""false_easting"",500000],
                PARAMETER[""false_northing"",0],
                AUTHORITY[""EPSG"",""25832""],
                AXIS[""Easting"",EAST],
                AXIS[""Northing"",NORTH]]";


            // WGS 84
            string wgsWkt = @"
                GEOGCS[""GCS_WGS_1984"",
                    DATUM[""D_WGS_1984"",SPHEROID[""WGS_1984"",6378137,298.257223563]],
                    PRIMEM[""Greenwich"",0],
                    UNIT[""Degree"",0.0174532925199433]
                ]";

            var cf = new ProjNet.CoordinateSystems.CoordinateSystemFactory();
            _fromCS = cf.CreateFromWkt(utmWkt);
            _toCS = cf.CreateFromWkt(wgsWkt);

            _ctfac = new CoordinateTransformationFactory();
            _trans = _ctfac.CreateFromCoordinateSystems(_fromCS, _toCS);
        }

        public double[] ConvertFromUTM32NToWGS84(double x, double y)
        {
            // Transform point to WGS84 latitude longitude 
            double[] fromPoint = new double[] { x, y };
            double[] toPoint = _trans.MathTransform.Transform(fromPoint);

            return toPoint;
        }

        public Envelope ConvertFromUTM32NToWGS84(Envelope envelope)
        {
            var geometryFactory = new GeometryFactory();
            var envelopeGeometry = geometryFactory.ToGeometry(envelope);
            var envelopeCoordinates = _trans.MathTransform
                .TransformList(envelopeGeometry.Coordinates.Select(x => new double[] { x.X, x.Y}).ToList())
                .Select(x => new Coordinate(x[0], x[1]));

            return new Envelope(envelopeCoordinates);
        }

        public CoordinateConversionResult ConvertGeoJsonLineStringsToWgs84(string[] geoJsonLineStrings)
        {
            if (geoJsonLineStrings == null)
                return null;

            var geoJsonReader = new GeoJsonReader();
            var geoJsonWriter = new GeoJsonWriter();

            Envelope wgs84BoundingBox = new();

            Envelope etrs89BoundingBox = new();


            List<string> wgs85geoJsonStrings = new();

            foreach (var inputGeoJson in geoJsonLineStrings)
            {
                var line = geoJsonReader.Read<LineString>("{ \"type\": \"LineString\",\"coordinates\":" + inputGeoJson + " }");

                etrs89BoundingBox.ExpandToInclude(line.EnvelopeInternal);

                foreach (var coord in line.Coordinates)
                {
                    var conversionResult = ConvertFromUTM32NToWGS84(coord.X, coord.Y);
                    coord.X = conversionResult[0];
                    coord.Y = conversionResult[1];
                }

                line.GeometryChanged();

                wgs84BoundingBox.ExpandToInclude(line.EnvelopeInternal);

                var newGeoJson = geoJsonWriter.Write(line);

                newGeoJson = newGeoJson.Replace("{\"type\":\"LineString\",\"coordinates\":", "");
                newGeoJson = newGeoJson.Replace("}", "");

                wgs85geoJsonStrings.Add(newGeoJson);
            }

            return new CoordinateConversionResult(AddBoundingBoxMargin(wgs84BoundingBox), AddBoundingBoxMargin(etrs89BoundingBox), wgs85geoJsonStrings.ToArray());
        }


        private static Envelope AddBoundingBoxMargin(Envelope envelope)
        {
            var expandDistance = ((envelope.Width * _boundingBoxExtraMarginPercent) / 100);

            envelope.ExpandBy(expandDistance);

            return envelope;
        }
    }

    public class CoordinateConversionResult
    {
        public Envelope WGS84BoundingBox { get; }
        public Envelope ETRS89BoundingBox { get;  }
        public string[] WGS84GeoJsonStrings { get; }

        public CoordinateConversionResult(Envelope wGS84BoundingBox, Envelope eTRS89BoundingBox, string[] wGS84GeoJsonStrings)
        {
            WGS84BoundingBox = wGS84BoundingBox;
            ETRS89BoundingBox = eTRS89BoundingBox;
            WGS84GeoJsonStrings = wGS84GeoJsonStrings;
        }
    }
}
