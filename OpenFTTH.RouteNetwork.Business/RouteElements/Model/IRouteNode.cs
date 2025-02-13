using OpenFTTH.Events.RouteNetwork.Infos;

namespace OpenFTTH.RouteNetwork.Business.RouteElements.Model
{
    public interface IRouteNode : IRouteNetworkElement
    {
        public RouteNodeInfo? RouteNodeInfo { get; set; }

        public double X { get; }

        public double Y { get; }
    }
}
