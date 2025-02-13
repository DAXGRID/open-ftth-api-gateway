using OpenFTTH.Events;
using OpenFTTH.RouteNetwork.API.Model;
using System;

namespace OpenFTTH.RouteNetwork.Business.Interest.Events
{
    public record WalkOfInterestRouteNetworkElementsModified : EventStoreBaseEvent
    {
        public Guid InterestId { get; }
        public RouteNetworkElementIdList RouteNetworkElementIds { get; }

        public WalkOfInterestRouteNetworkElementsModified(Guid interestId, RouteNetworkElementIdList routeNetworkElementIds)
        {
            this.Timestamp = DateTime.UtcNow;
            InterestId = interestId;
            RouteNetworkElementIds = routeNetworkElementIds;
        }
    }
}
