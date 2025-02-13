using OpenFTTH.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events
{
    public record NodeContainerVerticalAlignmentReversed : EventStoreBaseEvent
    {
        public Guid NodeContainerId { get; }

        public NodeContainerVerticalAlignmentReversed(Guid nodeContainerId)
        {
            NodeContainerId = nodeContainerId;
        }
    }
}
