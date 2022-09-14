using GraphQL.Types;
using OpenFTTH.APIGateway.GraphQL.RouteNetwork.Mutations;
using OpenFTTH.APIGateway.GraphQL.Work.Mutations;

namespace OpenFTTH.APIGateway.GraphQL.Root
{
    public class OpenFTTHMutations : ObjectGraphType
    {
        public OpenFTTHMutations()
        {
            Description = "Entry point for sending mutations to the various underlying services";

            Field<SpanEquipmentMutations>("spanEquipment").Resolve(_ => new { });

            Field<TerminalEquipmentMutations>("terminalEquipment").Resolve(_ => new { });

            Field<NodeContainerMutations>("nodeContainer").Resolve(_ => new { });

            Field<UserWorkContextMutations>("userContext").Resolve(_ => new { });

            Field<SchematicMutations>("schematic").Resolve(_ => new { });

            Field<TestDataMutations>("testData").Resolve(_ => new { });
        }
    }
}
