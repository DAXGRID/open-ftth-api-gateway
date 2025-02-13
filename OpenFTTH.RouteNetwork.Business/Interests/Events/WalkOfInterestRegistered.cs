using OpenFTTH.Events;
using OpenFTTH.RouteNetwork.API.Model;
using System;

namespace OpenFTTH.RouteNetwork.Business.Interest.Events
{
    public record WalkOfInterestRegistered : EventStoreBaseEvent
    {
        public RouteNetworkInterest Interest { get; }
        public WalkOfInterestRegistered(RouteNetworkInterest interest)
        {
            this.Timestamp = DateTime.UtcNow;
            this.Interest = interest;
        }
    }
}
