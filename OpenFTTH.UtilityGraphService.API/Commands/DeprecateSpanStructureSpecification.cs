using FluentResults;
using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record DeprecateSpanStructureSpecification : BaseCommand, ICommand<Result>
    {
        public Guid SpanStructureSpecificationId { get; }

        public DeprecateSpanStructureSpecification(Guid correlationId, UserContext userContext, Guid spanStructureSpecificationId) : base(correlationId, userContext)
        {
            SpanStructureSpecificationId = spanStructureSpecificationId;
        }
    }
}
