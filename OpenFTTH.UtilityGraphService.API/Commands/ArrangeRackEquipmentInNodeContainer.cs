using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record ArrangeRackEquipmentInNodeContainer : BaseCommand, ICommand<Result>
    {
        public Guid NodeContainerId { get; }
        public Guid TerminalEquipmentId { get; }
        public RackEquipmentArrangeMethodEnum ArrangeMethod { get; }
        public int NumberOfRackPositions { get; }

        public ArrangeRackEquipmentInNodeContainer(Guid correlationId, UserContext userContext, Guid nodeContainerId, Guid terminalEquipmentId, RackEquipmentArrangeMethodEnum arrangeMethod, int numberOfRackPositions) : base(correlationId, userContext)
        {
            NodeContainerId = nodeContainerId;
            TerminalEquipmentId = terminalEquipmentId;
            ArrangeMethod = arrangeMethod;
            NumberOfRackPositions = numberOfRackPositions;
        }
    }
}
