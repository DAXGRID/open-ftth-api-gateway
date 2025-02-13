using DAX.ObjectVersioning.Graph;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.Events.RouteNetwork.Infos;
using System;
using System.Globalization;

namespace OpenFTTH.RouteNetwork.Business.RouteElements.Model
{
    public class RouteNode : GraphNode, IRouteNode
    {
        public RouteNodeInfo? RouteNodeInfo { get; set; }
        public NamingInfo? NamingInfo { get; set; }
        public LifecycleInfo? LifecycleInfo { get; set; }
        public SafetyInfo? SafetyInfo { get; set; }
        public MappingInfo? MappingInfo { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public string Coordinates
        {
            get
            {
                return "[" + X.ToString(CultureInfo.InvariantCulture) + "," + Y.ToString(CultureInfo.InvariantCulture) + "]";
            }

            set
            {
                var coordArray = DoubleArrayFromCoordinateString(value);

                if (coordArray.Length < 2)
                {
                    throw new ApplicationException($"Invalid linestring: '${value}' Expected a coordinate with at least two values (X,Y).");
                }

                X = coordArray[0];
                Y = coordArray[1];
            }
        }

        public double[] CoordArray
        {
            get {
                return new double[] { X, Y };
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


        public RouteNode(Guid id, string coordinates) : base(id)
        {
            this.Coordinates = coordinates;
        }
    }
}
