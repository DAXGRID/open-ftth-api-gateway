using OpenFTTH.Core;
using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    /// <summary>
    /// Immutable structure holding a terminal
    /// Please keep this structure as light as possible, as millions of these guys will be exchanged and cached in memory.
    /// </summary>
    public record Terminal : IIdentifiedObject
    {
        public Guid Id { get; }
        public string Name { get; }
        public TerminalDirectionEnum Direction { get; }
        public bool IsPigtail { get; }
        public bool IsSplice { get; }
        public string? ConnectorType { get; }
        public Guid FromSegmentId { get; init; }
        public Guid ToSegmentId { get; init; }
        public Guid? InternalConnectivityNodeId { get; init; }
        public string? Description => null;

        public Terminal(Guid id, string name, TerminalDirectionEnum direction, bool isPigtail, bool isSplice, string? connectorType, Guid? internalConnectivityNodeId)
        {
            Id = id;
            Name = name;
            Direction = direction;
            IsPigtail = isPigtail;
            IsSplice = isSplice;
            ConnectorType = connectorType;
            InternalConnectivityNodeId = internalConnectivityNodeId;
        }
    }
}
