using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.CQRS;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;

namespace OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types
{
    public class EquipmentTagType : ObjectGraphType<EquipmentTag>
    {
        public EquipmentTagType(ILogger<EquipmentTagType> logger, IQueryDispatcher queryDispatcher)
        {
            Field(x => x.TerminalOrSpanId, type: typeof(IdGraphType)).Description("Master Resource Identifier UUID Property");
            Field(x => x.Tags, type: typeof(ListGraphType<StringGraphType>)).Description("Tags");
            Field(x => x.Comment, type: typeof(StringGraphType)).Description("Comment");
        }
    }

    public class EquipmentDisplayTagType : ObjectGraphType<EquipmentDisplayTag>
    {
        public EquipmentDisplayTagType(ILogger<EquipmentDisplayTagType> logger, IQueryDispatcher queryDispatcher)
        {
            Field(x => x.TerminalOrSpanId, type: typeof(IdGraphType)).Description("Master Resource Identifier UUID Property");
            Field(x => x.DisplayName, type: typeof(StringGraphType)).Description("Display name");
            Field(x => x.Tags, type: typeof(ListGraphType<StringGraphType>)).Description("Tags");
            Field(x => x.Comment, type: typeof(StringGraphType)).Description("Comment");
        }
    }
}
