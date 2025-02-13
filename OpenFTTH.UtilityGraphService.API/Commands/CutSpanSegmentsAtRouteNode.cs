using FluentResults;
using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record CutSpanSegmentsAtRouteNode : BaseCommand, ICommand<Result>
    {
        public Guid RouteNodeId { get; }
        public Guid[] SpanSegmentsToCut { get; }

        public CutSpanSegmentsAtRouteNode(Guid correlationId, UserContext userContext, Guid routeNodeId, Guid[] spanSegmentsToCut) : base(correlationId, userContext)
        {
            RouteNodeId = routeNodeId;
            SpanSegmentsToCut = spanSegmentsToCut;
        }
    }
}
