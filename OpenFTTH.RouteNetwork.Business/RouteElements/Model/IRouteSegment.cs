using OpenFTTH.Events.RouteNetwork.Infos;

namespace OpenFTTH.RouteNetwork.Business.RouteElements.Model
{
    public interface IRouteSegment : IRouteNetworkElement
    {
        public RouteSegmentInfo? RouteSegmentInfo { get; set; }
    }
}
