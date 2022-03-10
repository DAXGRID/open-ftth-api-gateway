using GraphQL;
using GraphQL.Resolvers;
using GraphQL.Server.Transports.Subscriptions.Abstractions;
using GraphQL.Types;
using Microsoft.Extensions.Options;
using OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types;
using OpenFTTH.APIGateway.Settings;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;
using System;

namespace OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Subscriptions
{
    public class TerminalEquipmentConnectivityUpdatedSubscription
    {
        private readonly TerminalEquipmentConnectivityObserver _terminalEquipmentConnectivityObserver;
        private readonly AuthSetting _authSetting;

        public TerminalEquipmentConnectivityUpdatedSubscription(TerminalEquipmentConnectivityObserver terminalEquipmentConnectivityObserver, IOptions<AuthSetting> authSetting)
        {
            _terminalEquipmentConnectivityObserver = terminalEquipmentConnectivityObserver;
            _authSetting = authSetting.Value;
        }

        public void AddFields(ObjectGraphType objectGraphType)
        {
            objectGraphType.AddField(new EventStreamFieldType
            {
                Name = "terminalEquipmentConnectivityUpdated",
                Type = typeof(TerminalEquipmentAZConnectivityViewModelType),
                Resolver = new FuncFieldResolver<TerminalEquipmentAZConnectivityViewModel>(context => context.Source as TerminalEquipmentAZConnectivityViewModel),
                Arguments = new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalEquipmentOrRackId" }
                ),
                Subscriber = new EventStreamResolver<TerminalEquipmentAZConnectivityViewModel>(context =>
                {
                    var messageHandlingContext = (MessageHandlingContext)context.UserContext;
                    var graphQLUserContext = messageHandlingContext.Get<GraphQLUserContext>("GraphQLUserContext");

                    if (_authSetting.Enable && !graphQLUserContext.User.Identity.IsAuthenticated)
                    {
                        context.Errors.Add(new ExecutionError("Not authorized"));
                        return null;
                    }

                    var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                    if (routeNodeId == Guid.Empty)
                    {
                        context.Errors.Add(new ExecutionError($"nameof(routeNodeId) guid cannot be empty."));
                        return null;
                    }

                    var terminalEquipmentOrRackId = context.GetArgument<Guid>("terminalEquipmentOrRackId");
                    if (terminalEquipmentOrRackId == Guid.Empty)
                    {
                        context.Errors.Add(new ExecutionError($"{nameof(terminalEquipmentOrRackId)} guid cannot be empty."));
                        return null;
                    }

                    return _terminalEquipmentConnectivityObserver.WhenViewNeedsUpdate(routeNodeId, terminalEquipmentOrRackId);
                }),
            });
        }
    }
}
