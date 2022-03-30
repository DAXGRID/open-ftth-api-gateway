using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.UtilityGraphService.API.Commands;

namespace OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types
{
    public class ConnectSpanSegmentToTerminalOperationType : ObjectGraphType<ConnectSpanSegmentToTerminalOperation>
    {
        public ConnectSpanSegmentToTerminalOperationType(ILogger<ConnectSpanSegmentToTerminalOperationType> logger)
        {
            Field(x => x.SpanSegmentId, type: typeof(IdGraphType)).Description("Span segment id");
            Field(x => x.TerminalId, type: typeof(IdGraphType)).Description("Terminal id id");
        }
    }

    public class ConnectSpanSegmentToTerminalOperationInputType : InputObjectGraphType<ConnectSpanSegmentToTerminalOperation>
    {
        public ConnectSpanSegmentToTerminalOperationInputType(ILogger<ConnectSpanSegmentToTerminalOperationInputType> logger)
        {
            Field(x => x.SpanSegmentId, type: typeof(IdGraphType)).Description("Span segment id");
            Field(x => x.TerminalId, type: typeof(IdGraphType)).Description("Terminal id id");
        }
    }
}
