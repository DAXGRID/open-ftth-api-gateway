using FluentResults;
using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record DisconnectSpanSegmentsAtRouteNode : BaseCommand, ICommand<Result>
    {
        public Guid RouteNodeId { get; }
        public Guid[] SpanSegmentsToDisconnect { get; }

        public DisconnectSpanSegmentsAtRouteNode(Guid correlationId, UserContext userContext, Guid routeNodeId, Guid[] spanSegmentsToDisconnect) : base(correlationId, userContext)
        {
            RouteNodeId = routeNodeId;
            SpanSegmentsToDisconnect = spanSegmentsToDisconnect;
        }
    }
}
