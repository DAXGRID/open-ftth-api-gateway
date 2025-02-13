using FluentResults;
using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record UpdateNodeContainerProperties : BaseCommand, ICommand<Result>
    {
        public Guid NodeContainerId { get; }

        public Guid? SpecificationId { get; init; }
        public Guid? ManufacturerId { get; init; }

        public UpdateNodeContainerProperties(Guid correlationId, UserContext userContext, Guid nodeContainerId) : base(correlationId, userContext)
        {
            NodeContainerId = nodeContainerId;
        }
    }
}
