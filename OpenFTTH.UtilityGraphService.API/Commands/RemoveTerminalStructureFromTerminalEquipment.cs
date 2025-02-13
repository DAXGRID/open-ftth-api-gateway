using FluentResults;
using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record RemoveTerminalStructureFromTerminalEquipment : BaseCommand, ICommand<Result>
    {
        public Guid RouteNodeId { get; }
        public Guid TerminalEquipmentId { get; }
        public Guid TerminalStructureId { get; }

        public RemoveTerminalStructureFromTerminalEquipment(Guid correlationId, UserContext userContext, Guid routeNodeId, Guid terminalEquipmentId, Guid terminalStructureId) : base(correlationId, userContext)
        {
            RouteNodeId = routeNodeId;
            TerminalEquipmentId = terminalEquipmentId;
            TerminalStructureId = terminalStructureId;
        }
    }
}
