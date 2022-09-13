using GraphQL;
using GraphQL.Resolvers;
using GraphQL.Types;
using Microsoft.Extensions.Options;
using OpenFTTH.APIGateway.GraphQL.Schematic.Types;
using OpenFTTH.APIGateway.Settings;
using OpenFTTH.Schematic.API.Model.DiagramLayout;
using System;

namespace OpenFTTH.APIGateway.GraphQL.Schematic.Subscriptions
{
    public class SchematicUpdatedSubscription
    {
        private readonly SchematicDiagramObserver _schematicDiagramObserver;
        private readonly AuthSetting _authSetting;

        public SchematicUpdatedSubscription(
            SchematicDiagramObserver schematicDiagramObserver,
            IOptions<AuthSetting> authSetting)
        {
            _schematicDiagramObserver = schematicDiagramObserver;
            _authSetting = authSetting.Value;
        }

        public void AddFields(ObjectGraphType objectGraphType)
        {
            objectGraphType.AddField(new FieldType
            {
                Name = "schematicDiagramUpdated",
                Type = typeof(DiagramType),
                Resolver = new FuncFieldResolver<Diagram>(context => context.Source as Diagram),
                Arguments = new QueryArguments(
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNetworkElementId" }
                ),
                StreamResolver = new SourceStreamResolver<Diagram>(context =>
                {
                    var routeNetworkElementId = context.GetArgument<Guid>("routeNetworkElementId");
                    return _schematicDiagramObserver.WhenDiagramNeedsUpdate(routeNetworkElementId);
                }),
            });
        }
    }
}
