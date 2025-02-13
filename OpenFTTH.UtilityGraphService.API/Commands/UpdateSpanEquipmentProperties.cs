using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record UpdateSpanEquipmentProperties : BaseCommand, ICommand<Result>
    {
        public Guid SpanEquipmentOrSegmentId { get; }

        public Guid? SpecificationId { get; init; }
        public Guid? ManufacturerId { get; init; }
        public NamingInfo? NamingInfo { get; init; }
        public MarkingInfo? MarkingInfo { get; init; }
        public AddressInfo? AddressInfo { get; set; }

        public UpdateSpanEquipmentProperties(Guid correlationId, UserContext userContext, Guid spanEquipmentOrSegmentId) : base(correlationId, userContext)
        {
            SpanEquipmentOrSegmentId = spanEquipmentOrSegmentId;
        }
    }
}
