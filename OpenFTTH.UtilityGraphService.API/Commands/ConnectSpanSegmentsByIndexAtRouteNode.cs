using FluentResults;
using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record ConnectSpanSegmentsByIndexAtRouteNode : BaseCommand, ICommand<Result>
    {
        public Guid RouteNodeId { get; }
        public Guid FromSpanEquipmentId { get; }
        public Guid ToSpanEquipmentId { get; }
        public ushort FromStructureIndex { get; }
        public ushort ToStructureIndex { get; }
        public int NumberOfUnits { get; }

        public ConnectSpanSegmentsByIndexAtRouteNode(Guid correlationId, UserContext userContext, Guid routeNodeId, Guid fromSpanEquipmentId, Guid toSpanEquipmentId, ushort fromStructureIndex, ushort toStructureIndex, int numberOfUnits) : base(correlationId, userContext)
        {
            RouteNodeId = routeNodeId;
            FromSpanEquipmentId = fromSpanEquipmentId;
            ToSpanEquipmentId = toSpanEquipmentId;
            FromStructureIndex = fromStructureIndex;
            ToStructureIndex = toStructureIndex;
            NumberOfUnits = numberOfUnits;
        }
    }
}
