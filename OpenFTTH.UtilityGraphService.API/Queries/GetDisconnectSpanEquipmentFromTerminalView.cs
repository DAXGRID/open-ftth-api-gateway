using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;
using System;

namespace OpenFTTH.UtilityGraphService.API.Queries
{
    public class GetDisconnectSpanEquipmentFromTerminalView : IQuery<Result<DisconnectSpanEquipmentFromTerminalView>> 
    {
        public Guid SpanSegmentId { get; }
        public Guid TerminalId { get; }

        public GetDisconnectSpanEquipmentFromTerminalView(Guid spanSegmentId, Guid terminalId)
        {
            SpanSegmentId = spanSegmentId;
            TerminalId = terminalId;
        }
    }
}
