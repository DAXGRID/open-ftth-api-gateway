using OpenFTTH.Events;
using System;

namespace OpenFTTH.RouteNetwork.Business.Interest.Events
{
    public record InterestUnregistered : EventStoreBaseEvent
    {
        public Guid InterestId { get; }
        public InterestUnregistered(Guid interestId)
        {
            this.Timestamp = DateTime.UtcNow;
            this.InterestId = interestId;
        }
    }
}
