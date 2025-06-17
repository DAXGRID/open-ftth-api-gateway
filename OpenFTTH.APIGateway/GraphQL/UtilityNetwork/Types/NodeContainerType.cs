using OpenFTTH.Results;
using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.CQRS;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using System;

namespace OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types
{
    public class NodeContainerType : ObjectGraphType<NodeContainer>
    {
        public NodeContainerType(ILogger<SpanEquipmentType> logger, IQueryDispatcher queryDispatcher)
        {
            Field(x => x.Id, type: typeof(IdGraphType)).Description("Master Resource Identifier UUID Property");
            Field(x => x.Name, type: typeof(StringGraphType)).Description("Short name");
            Field(x => x.Description, type: typeof(StringGraphType)).Description("Long description");
            Field(x => x.SpecificationId, type: typeof(IdGraphType)).Description("Span equipment specification id");
            Field(x => x.ManufacturerId, type: typeof(IdGraphType)).Description("Span equipment manufacturer id");

            Field<NodeContainerSpecificationType>("specification")
               .Description("The specification used to create the node container")
               .ResolveAsync(async context =>
               {
                   var queryResult = await queryDispatcher.HandleAsync<GetNodeContainerSpecifications, Result<LookupCollection<NodeContainerSpecification>>>(
                       new GetNodeContainerSpecifications());

                   return queryResult.Value[context.Source.SpecificationId];
               });

            Field<ManufacturerType>("manufacturer")
                .Description("The manufacturer of the node container")
                .ResolveAsync(async context =>
                {
                    if (context.Source.ManufacturerId == null || context.Source.ManufacturerId == Guid.Empty)
                        return null;

                    var queryResult = await queryDispatcher.HandleAsync<GetManufacturer, Result<LookupCollection<Manufacturer>>>(new GetManufacturer());

                    return queryResult.Value[context.Source.ManufacturerId.Value];
                });
        }
    }
}
