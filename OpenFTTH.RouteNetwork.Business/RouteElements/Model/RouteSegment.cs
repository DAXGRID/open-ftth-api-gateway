using DAX.ObjectVersioning.Graph;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.Events.RouteNetwork.Infos;
using System;
using System.Globalization;
using System.Text;

namespace OpenFTTH.RouteNetwork.Business.RouteElements.Model
{
    public class RouteSegment : GraphEdge, IRouteSegment
    {
        public RouteSegmentInfo? RouteSegmentInfo { get; set; }
        public NamingInfo? NamingInfo { get; set; }
        public LifecycleInfo? LifecycleInfo { get; set; }
        public SafetyInfo? SafetyInfo { get; set; }
        public MappingInfo? MappingInfo { get; set; }
        public double[] CoordArray { get; set; }

        public RouteSegment(Guid id, string coordinates, RouteNode fromNode, RouteNode toNode) : base(id, fromNode, toNode)
        {
            CoordArray = DoubleArrayFromCoordinateString(coordinates);

            if (CoordArray.Length < 4)
            {
                throw new ApplicationException($"Invalid linestring: '${coordinates}' Expected at least two coordinates.");
            }
        }

        public string Coordinates { 
            get
            {
                StringBuilder sb = new StringBuilder();

                sb.Append('[');

                for (int i = 0; i < CoordArray.Length; i+=2) 
                {
                    if (sb.Length > 1)
                        sb.Append(',');

                    sb.Append('[');
                    sb.Append(CoordArray[i].ToString(CultureInfo.InvariantCulture));
                    sb.Append(',');
                    sb.Append(CoordArray[i+1].ToString(CultureInfo.InvariantCulture));
                    sb.Append(']');
                }

                sb.Append(']');

                return sb.ToString();
            }

            set
            {
                CoordArray = DoubleArrayFromCoordinateString(value);
            }
        }

        public Envelope Extent

        {
            get
            {
                var extend = new Envelope();

                for (int i = 0; i < CoordArray.Length; i+=2)
                {
                    extend.ExpandToInclude(CoordArray[i], CoordArray[i+1]);
                }

                return extend;
            }
        }

        public double Length
        {
            get
            {
                double length = 0.0;

                for (int i = 2; i < CoordArray.Length; i += 2)
                {
                    var startX = CoordArray[i - 2];
                    var startY = CoordArray[i - 1];
                    var endX = CoordArray[i];
                    var endY = CoordArray[i + 1];

                    length += Math.Sqrt(Math.Pow((endY - startY), 2) + Math.Pow((endX - startX), 2));
                }

                return length;
            }
        }

     
        private static double[] DoubleArrayFromCoordinateString(string coordinates)
        {
            var coordSplit = coordinates.Replace("[", "").Replace("]", "").Replace(" ", "").Split(',');

            var coordArray = new double[coordSplit.Length];

            for (int i = 0; i < coordSplit.Length; i++)
            {
                coordArray[i] = Double.Parse(coordSplit[i], CultureInfo.InvariantCulture);
            }

            return coordArray;
        }
    }
}
