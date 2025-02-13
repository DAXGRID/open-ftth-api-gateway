using FluentResults;
using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record ReverseNodeContainerVerticalContentAlignment : BaseCommand, ICommand<Result>
    {
        public Guid NodeContainerId { get; }

        public ReverseNodeContainerVerticalContentAlignment(Guid correlationId, UserContext userContext, Guid nodeContainerId) : base(correlationId, userContext)
        {
            NodeContainerId = nodeContainerId;
        }
    }
}
