using GraphQL;
using GraphQL.Resolvers;
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

        public TerminalEquipmentConnectivityUpdatedSubscription(TerminalEquipmentConnectivityObserver terminalEquipmentConnectivityObserver, IOptions<AuthSetting> authSetting)
        {
            _terminalEquipmentConnectivityObserver = terminalEquipmentConnectivityObserver;
        }

        public void AddFields(ObjectGraphType objectGraphType)
        {
            objectGraphType.AddField(new FieldType
            {
                Name = "terminalEquipmentConnectivityUpdated",
                Type = typeof(TerminalEquipmentAZConnectivityViewModelType),
                Resolver = new FuncFieldResolver<TerminalEquipmentAZConnectivityViewModel>(context => context.Source as TerminalEquipmentAZConnectivityViewModel),
                Arguments = new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalEquipmentOrRackId" }
                ),
                StreamResolver = new SourceStreamResolver<TerminalEquipmentAZConnectivityViewModel>(context =>
                {
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
