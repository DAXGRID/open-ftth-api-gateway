using OpenFTTH.Events.Core.Infos;
using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public record TerminalEquipment : IEquipment
    {
        public Guid Id { get; }
        public Guid SpecificationId { get; init; }
        public Guid NodeContainerId { get; init; }
        public TerminalStructure[] TerminalStructures { get; init; }
        public Guid? ManufacturerId { get; init; }
        public NamingInfo? NamingInfo { get; init; }
        public LifecycleInfo? LifecycleInfo { get; init; }
        public AddressInfo? AddressInfo { get; init; }

        public string? Name => NamingInfo?.Name;
        public string? Description => NamingInfo?.Description;

        public TerminalEquipment(Guid id, Guid specificationId, Guid nodeContainerId, TerminalStructure[] terminalStructures, Guid? manufacturerId, NamingInfo? namingInfo, LifecycleInfo? lifecycleInfo, AddressInfo? addressInfo)
        {
            Id = id;
            SpecificationId = specificationId;
            NodeContainerId = nodeContainerId;
            TerminalStructures = terminalStructures;
            ManufacturerId = manufacturerId;
            NamingInfo = namingInfo;
            LifecycleInfo = lifecycleInfo;
            AddressInfo = addressInfo;
        }
    }
}
