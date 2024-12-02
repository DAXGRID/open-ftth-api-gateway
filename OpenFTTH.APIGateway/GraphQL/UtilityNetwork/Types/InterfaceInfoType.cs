using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;

namespace OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types
{
    public class InterfaceInfoType : ObjectGraphType<InterfaceInfo>
    {
        public InterfaceInfoType(ILogger<InterfaceInfo> logger)
        {
            Field(x => x.InterfaceType, type:typeof(StringGraphType)).Description("Interface type - i.e. GE");
            Field(x => x.SlotNumber, type: typeof(IntGraphType)).Description("Slot number");
            Field(x => x.SubSlotNumber, type: typeof(IntGraphType)).Description("Sub slot number");
            Field(x => x.PortNumber, type: typeof(IntGraphType)).Description("Port number");
            Field(x => x.CircuitName, type: typeof(StringGraphType)).Description("Circuit name");
        }
    }

    public class InterfaceInfoInputType : InputObjectGraphType<InterfaceInfo>
    {
        public InterfaceInfoInputType(ILogger<InterfaceInfo> logger)
        {
            Field(x => x.InterfaceType, type: typeof(StringGraphType)).Description("Interface type - i.e. GE");
            Field(x => x.SlotNumber, type: typeof(IntGraphType)).Description("Slot number");
            Field(x => x.SubSlotNumber, type: typeof(IntGraphType)).Description("Sub slot number");
            Field(x => x.PortNumber, type: typeof(IntGraphType)).Description("Port number");
            Field(x => x.CircuitName, type: typeof(StringGraphType)).Description("Circuit name");
        }
    }

}
