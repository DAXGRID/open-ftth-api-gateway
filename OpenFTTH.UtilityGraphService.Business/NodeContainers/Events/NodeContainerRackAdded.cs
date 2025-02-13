using OpenFTTH.Events;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers.Events
{
    public record NodeContainerRackAdded : EventStoreBaseEvent
    {

        public Guid NodeContainerId { get; }
        public Guid RackId { get; }
        public Guid RackSpecificationId { get; }
        public string RackName { get;}
        public int RackPosition { get; }
        public int RackHeightInUnits { get; }

        public NodeContainerRackAdded(Guid nodeContainerId, Guid rackId, Guid rackSpecificationId, string rackName, int rackPosition, int rackHeightInUnits)
        {
            NodeContainerId = nodeContainerId;
            RackId = rackId;
            RackSpecificationId = rackSpecificationId;
            RackName = rackName;
            RackPosition = rackPosition;
            RackHeightInUnits = rackHeightInUnits;
        }
    }
}
