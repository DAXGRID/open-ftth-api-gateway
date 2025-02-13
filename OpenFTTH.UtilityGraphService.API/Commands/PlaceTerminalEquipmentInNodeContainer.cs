using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record PlaceTerminalEquipmentInNodeContainer : BaseCommand, ICommand<Result>
    {
        public Guid NodeContainerId { get; }
        public Guid TerminalEquipmentId { get; }
        public Guid TerminalEquipmentSpecificationId { get; }
        public int NumberOfEquipments { get; }
        public int StartSequenceNumber { get; }
        public TerminalEquipmentNamingMethodEnum NamingMethod { get; }
        public NamingInfo? NamingInfo { get; }
        public Guid? ManufacturerId { get; init; }
        public LifecycleInfo? LifecycleInfo { get; init; }
        public AddressInfo? AddressInfo { get; init; }
        public SubrackPlacementInfo? SubrackPlacementInfo { get; init; }

        public PlaceTerminalEquipmentInNodeContainer(Guid correlationId, UserContext userContext, Guid nodeContainerId, Guid terminalEquipmentId, Guid terminalEquipmentSpecificationId, int numberOfEquipments, int startSequenceNumber, TerminalEquipmentNamingMethodEnum namingMethod, NamingInfo? namingInfo) : base(correlationId, userContext)
        {
            NodeContainerId = nodeContainerId;
            TerminalEquipmentId = terminalEquipmentId;
            TerminalEquipmentSpecificationId = terminalEquipmentSpecificationId;
            NumberOfEquipments = numberOfEquipments;
            StartSequenceNumber = startSequenceNumber;
            NamingMethod = namingMethod;
            NamingInfo = namingInfo;
        }
    }
}
