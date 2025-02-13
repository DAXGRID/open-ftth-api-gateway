using FluentResults;
using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record RemoveTerminalEquipment : BaseCommand, ICommand<Result>
    {
        public Guid TerminalEquipmentId { get; }

        public RemoveTerminalEquipment(Guid correlationId, UserContext userContext, Guid terminalEquipmentId) : base(correlationId, userContext)
        {
            TerminalEquipmentId = terminalEquipmentId;
        }
    }
}
