using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record AddTerminalEquipmentSpecification : BaseCommand, ICommand<Result>
    {
        public TerminalEquipmentSpecification Specification { get; }

        public AddTerminalEquipmentSpecification(Guid correlationId, UserContext userContext, TerminalEquipmentSpecification specification) : base(correlationId, userContext)
        {
            Specification = specification;
        }
    }
}
