using GraphQL;
using GraphQL.Resolvers;
using GraphQL.Types;
using OpenFTTH.APIGateway.GraphQL.Schematic.Types;
using OpenFTTH.Schematic.API.Model.DiagramLayout;
using System;

namespace OpenFTTH.APIGateway.GraphQL.Schematic.Subscriptions
{
    public class SchematicUpdatedSubscription
    {
        private readonly SchematicDiagramObserver _schematicDiagramObserver;

        public SchematicUpdatedSubscription(SchematicDiagramObserver schematicDiagramObserver)
        {
            _schematicDiagramObserver = schematicDiagramObserver;
        }

        public void AddFields(ObjectGraphType objectGraphType)
        {
            objectGraphType.AddField(new EventStreamFieldType
            {
                Name = "schematicDiagramUpdated",
                Type = typeof(DiagramType),
                Resolver = new FuncFieldResolver<Diagram>(context => context.Source as Diagram),
                Arguments = new QueryArguments(
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNetworkElementId" }
                ),
                Subscriber = new EventStreamResolver<Diagram>(context =>
                {
                    if (!Guid.TryParse(context.Arguments["routeNetworkElementId"].ToString(), out Guid routeNetworkElementId))
                    {
                        context.Errors.Add(new ExecutionError("Wrong value for guid"));
                        return null;
                    }

                    return _schematicDiagramObserver.WhenDiagramNeedsUpdate(routeNetworkElementId);
                }),
            });
        }
    }
}
