using FluentResults;
using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record PlaceRackInNodeContainer : BaseCommand, ICommand<Result>
    {
        public Guid NodeContainerId { get; }
        public Guid RackId { get; }
        public Guid RackSpecificationId { get; }
        public string RackName { get; }
        public int RackHeightInUnits { get; }
        public int? RackPosition { get; }

        public PlaceRackInNodeContainer(Guid correlationId, UserContext userContext, Guid nodeContainerId, Guid rackId, Guid rackSpecificationId, string rackName, int rackHeightInUnits) : base(correlationId, userContext)
        {
            NodeContainerId = nodeContainerId;
            RackId = rackId;
            RackSpecificationId = rackSpecificationId;
            RackName = rackName;
            RackHeightInUnits = rackHeightInUnits;
        }
    }
}
