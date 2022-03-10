using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;

namespace OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types
{
    public class ConnectivityFaceConnectionType : ObjectGraphType<ConnectivityFaceConnection>
    {
        public ConnectivityFaceConnectionType(ILogger<ConnectivityFaceConnectionType> logger)
        {
            Field(x => x.TerminalOrSegmentId, type: typeof(IdGraphType)).Description("Terminal or span segment id");
            Field(x => x.Name, type: typeof(StringGraphType)).Description("Name of fiber, pin, port etc.");
            Field(x => x.EndInfo, type: typeof(StringGraphType)).Description("End connectivity info");
            Field(x => x.IsConnected, type: typeof(BooleanGraphType)).Description("Is the terminal og segment connected to anything");
        }
    }
}
