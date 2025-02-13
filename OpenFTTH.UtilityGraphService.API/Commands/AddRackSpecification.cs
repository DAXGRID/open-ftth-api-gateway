using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record AddRackSpecification : BaseCommand, ICommand<Result>
    {
        public RackSpecification Specification { get; }

        public AddRackSpecification(Guid correlationId, UserContext userContext, RackSpecification specification) : base(correlationId, userContext)
        {
            Specification = specification;
        }
    }
}
