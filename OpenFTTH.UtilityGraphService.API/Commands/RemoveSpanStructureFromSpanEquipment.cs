using FluentResults;
using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record RemoveSpanStructureFromSpanEquipment : BaseCommand, ICommand<Result>
    {
        public Guid SpanSegmentId { get; }

        public RemoveSpanStructureFromSpanEquipment(Guid correlationId, UserContext userContext, Guid spanSegmentId) : base(correlationId, userContext)
        {
            SpanSegmentId = spanSegmentId;
        }
    }
}
