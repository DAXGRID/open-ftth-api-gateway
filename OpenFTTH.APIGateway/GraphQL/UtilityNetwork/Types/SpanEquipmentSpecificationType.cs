using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;

namespace OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types
{
    public class SpanEquipmentSpecificationType : ObjectGraphType<SpanEquipmentSpecification>
    {
        public SpanEquipmentSpecificationType(ILogger<ManufacturerType> logger)
        {
            Field(x => x.Id, type: typeof(IdGraphType)).Description("Master Resource Identifier UUID Property");
            Field(x => x.Category, type: typeof(StringGraphType)).Description("Category - i.e. Conduit, Fiber Cable etc.");
            Field(x => x.Name, type: typeof(StringGraphType)).Description("Short name");
            Field(x => x.Description, type: typeof(StringGraphType)).Description("Long description");
            Field(x => x.Deprecated, type: typeof(BooleanGraphType)).Description("Whereas the type of span equipment is still in use");
            Field(x => x.ManufacturerRefs, type: typeof(ListGraphType<IdGraphType>)).Description("Manufacturer providing products of the the specification");
            Field(x => x.IsFixed, type: typeof(BooleanGraphType)).Description("True is the span equipment has a fixed span structure - i.e. you cannot add or remove inner span stuctures.");
            Field(x => x.IsMultiLevel, type: typeof(BooleanGraphType)).Description("True is the span equipment has and/or allows a multi level structure - i.e. it already has a fixed inner span structure or it allows inner spans to be added in case of a non-fixed structure.");

            Field("OuterSpanStructureSpecificationId", x => x.RootTemplate.SpanStructureSpecificationId, type: typeof(IdGraphType))
                .Description("Root / outer span structure specification.");
        }
    }
}
