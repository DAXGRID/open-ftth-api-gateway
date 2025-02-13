using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record AddTerminalStructureSpecification : BaseCommand, ICommand<Result>
    {
        public TerminalStructureSpecification Specification { get; }

        public AddTerminalStructureSpecification(Guid correlationId, UserContext userContext, TerminalStructureSpecification specification) : base(correlationId, userContext)
        {
            Specification = specification;
        }
    }
}
