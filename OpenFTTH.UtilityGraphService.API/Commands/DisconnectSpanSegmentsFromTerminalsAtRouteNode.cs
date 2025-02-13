using FluentResults;
using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record DisconnectSpanSegmentsFromTerminalsAtRouteNode : BaseCommand, ICommand<Result>
    {
        public Guid RouteNodeId { get; }
        public DisconnectSpanSegmentFromTerminalOperation[] Disconnects { get; }
        public DisconnectSpanSegmentsFromTerminalsAtRouteNode(Guid correlationId, UserContext userContext, Guid routeNodeId, DisconnectSpanSegmentFromTerminalOperation[] disconnects) : base(correlationId, userContext)
        {
            RouteNodeId = routeNodeId;
            Disconnects = disconnects;
        }
    }

    public record DisconnectSpanSegmentFromTerminalOperation
    {
        public Guid SpanSegmentId { get; }
        public Guid TerminalId { get; }

        public DisconnectSpanSegmentFromTerminalOperation(Guid spanSegmentId, Guid terminalId)
        {
            SpanSegmentId = spanSegmentId;
            TerminalId = terminalId;
        }
    }
}
