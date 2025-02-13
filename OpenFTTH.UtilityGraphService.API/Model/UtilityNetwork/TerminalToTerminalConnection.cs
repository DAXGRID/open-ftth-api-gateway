using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public record TerminalToTerminalConnection
    {
        public Guid FromTerminalId { get; }
        public Guid ToTerminalId { get; }

        public TerminalToTerminalConnection(Guid fromTerminalId, Guid toTerminalId)
        {
            FromTerminalId = fromTerminalId;
            ToTerminalId = toTerminalId;
        }
    }
}
