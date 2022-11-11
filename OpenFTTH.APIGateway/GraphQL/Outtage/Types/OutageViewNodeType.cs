using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types;
using OpenFTTH.APIGateway.Util;
using OpenFTTH.UtilityGraphService.API.Model.Outage;

namespace OpenFTTH.APIGateway.GraphQL.Outage.Types
{
    public class OutageViewNodeType : ObjectGraphType<OutageViewNode>
    {
        public OutageViewNodeType(ILogger<ConnectivityTraceViewType> logger, UTM32WGS84Converter coordinateConverter)
        {
            Field(x => x.Id, type: typeof(IdGraphType)).Description("Id");
            Field(x => x.Label, type: typeof(StringGraphType)).Description("Label");
            Field(x => x.Value, type: typeof(StringGraphType)).Description("Value");
            Field(x => x.Description, type: typeof(StringGraphType)).Description("Description");
            Field(x => x.Expanded, type: typeof(BooleanGraphType)).Description("Expanded");
            Field(x => x.Nodes, type: typeof(ListGraphType<OutageViewNodeType>)).Description("Child nodes");
        }
    }
}
