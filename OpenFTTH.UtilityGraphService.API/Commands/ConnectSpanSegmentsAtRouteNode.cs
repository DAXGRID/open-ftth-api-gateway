using FluentResults;
using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record ConnectSpanSegmentsAtRouteNode : BaseCommand, ICommand<Result>
    {
        public Guid RouteNodeId { get; }
        public Guid[] SpanSegmentsToConnect { get; }

        public ConnectSpanSegmentsAtRouteNode(Guid correlationId, UserContext userContext, Guid routeNodeId, Guid[] spanSegmentsToConnect) : base(correlationId, userContext)
        {
            RouteNodeId = routeNodeId;
            SpanSegmentsToConnect = spanSegmentsToConnect;
        }
    }
}
