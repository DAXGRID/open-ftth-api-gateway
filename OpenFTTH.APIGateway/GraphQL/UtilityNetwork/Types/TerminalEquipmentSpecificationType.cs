using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;

namespace OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types
{
    public class TerminalEquipmentSpecificationType : ObjectGraphType<TerminalEquipmentSpecification>
    {
        public TerminalEquipmentSpecificationType(ILogger<TerminalEquipmentSpecificationType> logger)
        {
            Field(x => x.Id, type: typeof(IdGraphType)).Description("Master Resource Identifier UUID Property");
            Field(x => x.Category, type: typeof(StringGraphType)).Description("Category - i.e. splice closure, subrack etc");
            Field(x => x.Name, type: typeof(StringGraphType)).Description("Unique specification name");
            Field(x => x.ShortName, type: typeof(IntGraphType)).Description("Short name");
            Field(x => x.Description, type: typeof(StringGraphType)).Description("Long description");
            Field(x => x.Deprecated, type: typeof(BooleanGraphType)).Description("Whereas the type of terminal equipment is still in use");
            Field(x => x.ManufacturerRefs, type: typeof(ListGraphType<IdGraphType>)).Description("Manufacturer providing products of the the specification");
            Field(x => x.IsFixed, type: typeof(BooleanGraphType)).Description("True is the terminal equipment has a fixed span structure - i.e. you cannot add or remove inner stuctures.");
            Field(x => x.IsRackEquipment, type: typeof(BooleanGraphType)).Description("True is the terminal equipment can only be inserted in a rack.");
            Field(x => x.HeightInRackUnits, type: typeof(IntGraphType)).Description("Height in rack units. Only provided if a rack equipment.");
            Field(x => x.IsAddressable, type: typeof(BooleanGraphType)).Description("Can be related to an address");
            Field(x => x.IsCustomerTermination, type: typeof(IntGraphType)).Description("Is customer termination");
        }
    }
}
