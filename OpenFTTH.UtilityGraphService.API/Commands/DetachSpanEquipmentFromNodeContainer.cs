using FluentResults;
using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record DetachSpanEquipmentFromNodeContainer : BaseCommand, ICommand<Result>
    {
        public Guid SpanEquipmentOrSegmentId { get; }
        public Guid RouteNodeId { get; }

        public DetachSpanEquipmentFromNodeContainer(Guid correlationId, UserContext userContext, Guid spanEquipmentOrSegmentId, Guid routeNodeId) : base(correlationId, userContext)
        {
            SpanEquipmentOrSegmentId = spanEquipmentOrSegmentId;
            RouteNodeId = routeNodeId;
        }
    }
}
