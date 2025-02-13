using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record AddSpanEquipmentSpecification : BaseCommand, ICommand<Result>
    {
        public SpanEquipmentSpecification Specification { get; }

        public AddSpanEquipmentSpecification(Guid correlationId, UserContext userContext, SpanEquipmentSpecification specification) : base(correlationId, userContext)
        {
            Specification = specification;
        }
    }
}
