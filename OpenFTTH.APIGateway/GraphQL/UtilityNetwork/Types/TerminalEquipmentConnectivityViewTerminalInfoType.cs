using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;

namespace OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types
{
    public class TerminalEquipmentConnectivityViewTerminalInfoType : ObjectGraphType<TerminalEquipmentAZConnectivityViewTerminalInfo>
    {
        public TerminalEquipmentConnectivityViewTerminalInfoType(ILogger<TerminalEquipmentConnectivityViewTerminalInfoType> logger)
        {
            Field(x => x.Id, type: typeof(IdGraphType)).Description("Master Resource Identifier UUID Property");
            Field(x => x.Name, type: typeof(StringGraphType)).Description("Terminal name");
            Field(x => x.HasTags, type: typeof(BooleanGraphType)).Description("Terminal has tag(s)");
        }
    }
}
