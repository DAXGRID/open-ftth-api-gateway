using GraphQL.Types;
using OpenFTTH.APIGateway.GraphQL.Core.Types;
using OpenFTTH.Schematic.API.Model.DiagramLayout;

namespace OpenFTTH.APIGateway.GraphQL.Schematic.Types
{
    public class DiagramType : ObjectGraphType<Diagram>
    {
        public DiagramType()
        {
            Description = "Diagram";

            Field(x => x.DiagramObjects, type: typeof(ListGraphType<DiagramObjectType>)).Description("All diagram objects contained by the diagram.");

            Field(x => x.Envelope, type: typeof(EnvelopeType)).Description("The extent of the diagram.");
        }
    }
}
