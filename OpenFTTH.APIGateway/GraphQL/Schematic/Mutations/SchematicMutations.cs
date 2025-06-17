using OpenFTTH.Results;
using GraphQL;
using GraphQL.Types;
using OpenFTTH.APIGateway.CoreTypes;
using OpenFTTH.APIGateway.GraphQL.Core.Model;
using OpenFTTH.APIGateway.GraphQL.Schematic.Subscriptions;
using System;

namespace OpenFTTH.APIGateway.GraphQL.RouteNetwork.Mutations
{
    public class SchematicMutations : ObjectGraphType
    {
        public SchematicMutations(SchematicDiagramObserver schematicDiagramObserver)
        {
            Description = "Schematic mutations/commands";

            Field<CommandResultType>("triggerDiagramUpdate")
              .Description("Just to test the schematic diagram observer and subscriptions")
              .Arguments(new QueryArguments(
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNeworkElementId" }
              ))
              .Resolve(context =>
              {
                  var routeNeworkElementId = context.GetArgument<Guid>("routeNeworkElementId");

                  schematicDiagramObserver.Ping(routeNeworkElementId);

                  return new CommandResult(Result.Ok());
              });

        }
    }
}
