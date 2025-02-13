using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record UpdateTerminalEquipmentProperties : BaseCommand, ICommand<Result>
    {
        public Guid TerminalEquipmentId { get; }
        public Guid? SpecificationId { get; init; }
        public Guid? ManufacturerId { get; init; }
        public NamingInfo? NamingInfo { get; init; }
        public AddressInfo? AddressInfo { get; set; }
        public Guid? RackId { get; set; }
        public int? StartUnitPosition { get; set; }

        public UpdateTerminalEquipmentProperties(Guid correlationId, UserContext userContext, Guid terminalEquipmentId) : base(correlationId, userContext)
        {
            TerminalEquipmentId = terminalEquipmentId;
        }
    }
}
