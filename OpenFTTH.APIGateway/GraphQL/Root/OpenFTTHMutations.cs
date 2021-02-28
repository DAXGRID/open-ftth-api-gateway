﻿using GraphQL.Types;
using OpenFTTH.APIGateway.GraphQL.RouteNetwork.Mutations;
using OpenFTTH.APIGateway.GraphQL.Work.Mutations;

namespace OpenFTTH.APIGateway.GraphQL.Root
{

    public class OpenFTTHMutations : ObjectGraphType
    {
        public OpenFTTHMutations()
        {
            Description = "Entry point for sending mutations to the various underlying services";

            Field<RouteNodeMutations>("routeNode", resolve: context => new { });
            Field<SpanEquipmentMutations>("spanEquipment", resolve: context => new { });
            Field<RouteSegmentMutations>("routeSegment", resolve: context => new { });
            Field<UserWorkContextMutations>("userContext", resolve: context => new { });
            Field<SchematicMutations>("schematic", resolve: context => new { });
        }
    }

}
