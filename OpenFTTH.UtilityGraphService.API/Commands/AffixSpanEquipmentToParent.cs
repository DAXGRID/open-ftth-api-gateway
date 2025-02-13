using FluentResults;
using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record AffixSpanEquipmentToParent : BaseCommand, ICommand<Result>
    {
        public Guid RouteNodeId { get; }
        public Guid ChildSpanSegmentId { get; }
        public Guid ParentSpanSegmentId { get; }

        public AffixSpanEquipmentToParent(Guid correlationId, UserContext userContext, Guid routeNodeId, Guid childSpanSegmentId1, Guid parentSpanSegmentId2) : base(correlationId, userContext)
        {
            RouteNodeId = routeNodeId;
            ChildSpanSegmentId = childSpanSegmentId1;
            ParentSpanSegmentId = parentSpanSegmentId2;
        }
    }
}
