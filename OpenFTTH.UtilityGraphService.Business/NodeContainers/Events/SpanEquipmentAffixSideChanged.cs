using OpenFTTH.Events;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers.Events
{
    public record SpanEquipmentAffixSideChanged : EventStoreBaseEvent
    {
        public Guid SpanEquipmentId { get; }
        public Guid NodeContainerId { get; }
        public NodeContainerSideEnum NodeContainerIngoingSide { get; }

        public SpanEquipmentAffixSideChanged(Guid spanEquipmentId, Guid nodeContainerId, NodeContainerSideEnum nodeContainerIngoingSide)
        {
            SpanEquipmentId = spanEquipmentId;
            NodeContainerId = nodeContainerId;
            NodeContainerIngoingSide = nodeContainerIngoingSide;
        }
    }
}
