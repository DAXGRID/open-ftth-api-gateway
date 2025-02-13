using OpenFTTH.Core;
using System;
using System.Linq;

namespace OpenFTTH.UtilityGraphService.API.Model.Trace
{
    public record UtilityNetworkTraceResult : IIdentifiedObject
    {
        public Guid Id { get; }
        public Guid? FromTerminalId { get; }
        public Guid? ToTerminalId { get; }
        public Guid[] SpanSegmentIds { get; }

        public string? Name => null;
        public string? Description => null;

        public UtilityNetworkTraceResult(Guid spanSegmentId, Guid? fromTerminalId, Guid? toTerminalId, Guid[] spanSegmentIds)
        {
            Id = spanSegmentId;
            FromTerminalId = fromTerminalId;
            ToTerminalId = toTerminalId;
            SpanSegmentIds = spanSegmentIds;
        }

        public UtilityNetworkTraceResult Reverse()
        {
            return new UtilityNetworkTraceResult(Id, ToTerminalId, FromTerminalId, SpanSegmentIds.Reverse().ToArray());
        }
    }
}
