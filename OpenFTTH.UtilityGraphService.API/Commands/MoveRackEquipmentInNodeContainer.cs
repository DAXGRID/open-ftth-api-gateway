using FluentResults;
using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record MoveRackEquipmentInNodeContainer : BaseCommand, ICommand<Result>
    {
        public Guid NodeContainerId { get; }
        public Guid TerminalEquipmentId { get; }
        public Guid MoveToRackId { get; }
        public int MoveToRackPosition { get; }

        public MoveRackEquipmentInNodeContainer(Guid correlationId, UserContext userContext, Guid nodeContainerId, Guid terminalEquipmentId, Guid moveToRackId, int moveToRackPosition) : base(correlationId, userContext)
        {
            NodeContainerId = nodeContainerId;
            TerminalEquipmentId = terminalEquipmentId;
            MoveToRackId = moveToRackId;
            MoveToRackPosition = moveToRackPosition;
        }
    }
}
