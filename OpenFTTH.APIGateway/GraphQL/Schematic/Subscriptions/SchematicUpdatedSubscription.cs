using GraphQL;
using GraphQL.Resolvers;
using GraphQL.Server.Transports.Subscriptions.Abstractions;
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

        public SchematicUpdatedSubscription(SchematicDiagramObserver schematicDiagramObserver, IOptions<AuthSetting> authSetting)
        {
            _schematicDiagramObserver = schematicDiagramObserver;
            _authSetting = authSetting.Value;
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
                    var messageHandlingContext = (MessageHandlingContext)context.UserContext;
                    var graphQLUserContext = messageHandlingContext.Get<GraphQLUserContext>("GraphQLUserContext");

                    if (_authSetting.Enable && !graphQLUserContext.User.Identity.IsAuthenticated)
                    {
                        context.Errors.Add(new ExecutionError("Not authorized"));
                        return null;
                    }

                    var routeNetworkElementId = context.GetArgument<Guid>("routeNetworkElementId");
                    return _schematicDiagramObserver.WhenDiagramNeedsUpdate(routeNetworkElementId);
                }),
            });
        }
    }
}
