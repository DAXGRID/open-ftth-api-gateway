using FluentResults;
using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record RemoveNodeContainerFromRouteNetwork : BaseCommand, ICommand<Result>
    {
        public Guid NodeContainerId { get; }

        public RemoveNodeContainerFromRouteNetwork(Guid correlationId, UserContext userContext, Guid nodeContainerId) : base(correlationId, userContext)
        {
            NodeContainerId = nodeContainerId;
        }
    }
}
