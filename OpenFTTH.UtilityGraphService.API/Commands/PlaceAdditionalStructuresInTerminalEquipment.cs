using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record PlaceAdditionalStructuresInTerminalEquipment : BaseCommand, ICommand<Result>
    {
        public Guid RouteNodeId { get; }
        public Guid TerminalEquipmentId { get; }
        public Guid StructureSpecificationId { get;  }
        public int Position { get; }
        public int NumberOfStructures { get; }
        public InterfaceInfo? InterfaceInfo { get; set; }

        public PlaceAdditionalStructuresInTerminalEquipment(Guid correlationId, UserContext userContext, Guid routeNodeId, Guid terminalEquipmentId, Guid structureSpecificationId, int position, int numberOfStructures) : base(correlationId, userContext)
        {
            RouteNodeId = routeNodeId;
            TerminalEquipmentId = terminalEquipmentId;
            StructureSpecificationId = structureSpecificationId;
            Position = position;
            NumberOfStructures = numberOfStructures;
        }
    }
}
