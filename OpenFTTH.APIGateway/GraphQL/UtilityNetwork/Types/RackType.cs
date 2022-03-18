using FluentResults;
using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.CQRS;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;

namespace OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types
{
    public class RackType : ObjectGraphType<Rack>
    {
        public RackType(ILogger<SpanEquipmentType> logger, IQueryDispatcher queryDispatcher)
        {
            Field(x => x.Id, type: typeof(IdGraphType)).Description("Master Resource Identifier UUID Property");
            Field(x => x.Name, type: typeof(StringGraphType)).Description("Rack name");
            Field(x => x.HeightInUnits, type: typeof(IntGraphType)).Description("Height in rack units");
            Field(x => x.SpecificationId, type: typeof(IdGraphType)).Description("Rack specification id");

            FieldAsync<RackSpecificationType>(
               name: "specification",
               description: "The specification used to create the rack",
               resolve: async context =>
               {
                   var queryResult = await queryDispatcher.HandleAsync<GetRackSpecifications, Result<LookupCollection<RackSpecification>>>(new GetRackSpecifications());
                   return queryResult.Value[context.Source.SpecificationId];
               }
            );
        }
    }
}
