using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record UpdateTerminalStructureProperties : BaseCommand, ICommand<Result>
    {
        public Guid TerminalEquipmentId { get; }
        public Guid TerminalStructureId { get; }
        public Guid StructureSpecificationId { get; set; }
        public int Position { get; set; }
        public InterfaceInfo? InterfaceInfo { get; set; }
             
        public UpdateTerminalStructureProperties(Guid correlationId, UserContext userContext, Guid terminalEquipmentId, Guid terminalStructureId) : base(correlationId, userContext)
        {
            TerminalEquipmentId = terminalEquipmentId;
            TerminalStructureId = terminalStructureId;
        }
    }
}
