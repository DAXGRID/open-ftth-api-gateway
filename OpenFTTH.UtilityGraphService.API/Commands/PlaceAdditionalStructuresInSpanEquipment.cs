using FluentResults;
using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record PlaceAdditionalStructuresInSpanEquipment : BaseCommand, ICommand<Result>
    {
        public Guid SpanEquipmentId { get; }

        public Guid[] StructureSpecificationIds { get;  }

        public PlaceAdditionalStructuresInSpanEquipment(Guid correlationId, UserContext userContext, Guid spanEquipmentId, Guid[] structureSpecificationIds) : base(correlationId, userContext)
        {
            SpanEquipmentId = spanEquipmentId;
            StructureSpecificationIds = structureSpecificationIds;
        }
    }
}
