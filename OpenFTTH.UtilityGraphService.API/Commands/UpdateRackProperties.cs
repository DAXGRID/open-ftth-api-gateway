using FluentResults;
using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record UpdateRackProperties : BaseCommand, ICommand<Result>
    {
        public Guid RouteNodeId { get; }
        public Guid RackId { get; }
        public string? Name { get; init; }
        public Guid? SpecificationId { get; init; }
        public int? HeightInUnits { get; init; }

        public UpdateRackProperties(Guid correlationId, UserContext userContext, Guid routeNodeId, Guid rackId) : base(correlationId, userContext)
        {
            RouteNodeId = routeNodeId;
            RackId = rackId;
        }
    }
}
