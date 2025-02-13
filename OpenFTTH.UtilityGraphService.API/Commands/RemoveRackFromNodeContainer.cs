using FluentResults;
using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record RemoveRackFromNodeContainer : BaseCommand, ICommand<Result>
    {
        public Guid RouteNodeId { get; }
        public Guid RackId { get; }

        public RemoveRackFromNodeContainer(Guid correlationId, UserContext userContext, Guid routeNodeId, Guid rackId) : base(correlationId, userContext)
        {
            RouteNodeId = routeNodeId;
            RackId = rackId;
        }
    }
}
