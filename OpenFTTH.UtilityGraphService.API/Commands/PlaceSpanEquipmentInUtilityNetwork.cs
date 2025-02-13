using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record PlaceSpanEquipmentInUtilityNetwork : BaseCommand, ICommand<Result>
    {
        public Guid SpanEquipmentId { get; }
        public Guid SpanEquipmentSpecificationId { get; }
        public RoutingHop[] RoutingHops { get; }
        public Guid? ManufacturerId { get; init; }
        public NamingInfo? NamingInfo { get; init; }
        public LifecycleInfo? LifecycleInfo { get; init; }
        public MarkingInfo? MarkingInfo { get; init; }
        public AddressInfo? AddressInfo { get; init; }

        public PlaceSpanEquipmentInUtilityNetwork(Guid correlationId, UserContext userContext, Guid spanEquipmentId, Guid spanEquipmentSpecificationId, RoutingHop[] routingHops) : base(correlationId, userContext)
        {
            this.SpanEquipmentId = spanEquipmentId;
            this.SpanEquipmentSpecificationId = spanEquipmentSpecificationId;
            this.RoutingHops = routingHops;
        }
    }
}
