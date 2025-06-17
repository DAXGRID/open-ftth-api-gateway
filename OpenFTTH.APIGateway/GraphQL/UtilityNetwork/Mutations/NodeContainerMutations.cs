using OpenFTTH.Results;
using GraphQL;
using GraphQL.Types;
using OpenFTTH.APIGateway.CoreTypes;
using OpenFTTH.APIGateway.GraphQL.Addresses.Types;
using OpenFTTH.APIGateway.GraphQL.Core.Model;
using OpenFTTH.APIGateway.GraphQL.RouteNetwork.Types;
using OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types;
using OpenFTTH.APIGateway.Util;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;
using OpenFTTH.APIGateway.GraphQL.Work;

namespace OpenFTTH.APIGateway.GraphQL.RouteNetwork.Mutations
{
    public class NodeContainerMutations : ObjectGraphType
    {
        public NodeContainerMutations(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IEventStore eventStore)
        {
            Description = "Node container mutations";

            Field<CommandResultType>("placeNodeContainerInRouteNetwork")
                .Description("Place a node container (i.e. conduit closure, well, cabinet whatwever) in a route network node")
                .Arguments(new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "nodeContainerId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "nodeContainerSpecificationId" },
                    new QueryArgument<IdGraphType> { Name = "manufacturerId" }
                ))
                .ResolveAsync(async context =>
                {
                    var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                    var nodeContainerId = context.GetArgument<Guid>("nodeContainerId");
                    var nodeContainerSpecificationId = context.GetArgument<Guid>("nodeContainerSpecificationId");
                    var manufacturerId = context.GetArgument<Guid>("manufacturerId");

                    var userContext = context.UserContext as GraphQLUserContext;
                    var userName = userContext.Username;

                    Guid correlationId = Guid.NewGuid();

                    // Get the users current work task (will fail, if user has not selected a work task)
                    var currentWorkTaskIdResult = WorkQueryHelper.GetUserCurrentWorkId(userName, queryDispatcher);

                    if (currentWorkTaskIdResult.IsFailed)
                        return new CommandResult(currentWorkTaskIdResult);

                    var commandUserContext = new UserContext(userName, currentWorkTaskIdResult.Value)
                    {
                        EditingRouteNodeId = routeNodeId
                    };

                    // First register the walk in the route network where the client want to place the node container
                    var nodeOfInterestId = Guid.NewGuid();
                    var walk = new RouteNetworkElementIdList();
                    var registerNodeOfInterestCommand = new RegisterNodeOfInterest(correlationId, commandUserContext, nodeOfInterestId, routeNodeId);

                    var registerNodeOfInterestCommandResult = await commandDispatcher.HandleAsync<RegisterNodeOfInterest, Result<RouteNetworkInterest>>(registerNodeOfInterestCommand);

                    if (registerNodeOfInterestCommandResult.IsFailed)
                    {
                        return new CommandResult(registerNodeOfInterestCommandResult);
                    }

                    // Now place the conduit in the walk
                    var placeNodeContainerCommand = new PlaceNodeContainerInRouteNetwork(correlationId, commandUserContext, nodeContainerId, nodeContainerSpecificationId, registerNodeOfInterestCommandResult.Value)
                    {
                        ManufacturerId = manufacturerId,
                        LifecycleInfo = new LifecycleInfo(DeploymentStateEnum.InService, null, null)
                    };

                    var placeNodeContainerResult = await commandDispatcher.HandleAsync<PlaceNodeContainerInRouteNetwork, Result>(placeNodeContainerCommand);

                    // Unregister interest if place node container failed
                    if (placeNodeContainerResult.IsFailed)
                    {
                        var unregisterCommandResult = await commandDispatcher.HandleAsync<UnregisterInterest,
                            Result>(new UnregisterInterest(correlationId, commandUserContext, nodeOfInterestId));

                        if (unregisterCommandResult.IsFailed)
                            return new CommandResult(unregisterCommandResult);
                    }

                    return new CommandResult(placeNodeContainerResult);
                });

            Field<CommandResultType>("reverseVerticalContentAlignment")
                .Description("Toggle whether the content in the node container should be drawed from bottom up or top down")
                .Arguments(new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "nodeContainerId" }
                ))
                .ResolveAsync(async context =>
                {
                    var userContext = context.UserContext as GraphQLUserContext;
                    var userName = userContext.Username;

                    Guid correlationId = Guid.NewGuid();

                    // Get the users current work task (will fail, if user has not selected a work task)
                    var currentWorkTaskIdResult = WorkQueryHelper.GetUserCurrentWorkId(userName, queryDispatcher);

                    if (currentWorkTaskIdResult.IsFailed)
                        return new CommandResult(currentWorkTaskIdResult);

                    var commandUserContext = new UserContext(userName, currentWorkTaskIdResult.Value);

                    var nodeContainerId = context.GetArgument<Guid>("nodeContainerId");

                    var reverseAlignmentCmd = new ReverseNodeContainerVerticalContentAlignment(correlationId, commandUserContext, nodeContainerId);

                    var reverseAlignmentCmdResult = await commandDispatcher.HandleAsync<ReverseNodeContainerVerticalContentAlignment, Result>(reverseAlignmentCmd);

                    return new CommandResult(reverseAlignmentCmdResult);
                });

            Field<CommandResultType>("updateProperties")
                .Description("Mutation that can be used to change the node container specification and/or manufacturer")
                .Arguments(new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "nodeContainerId" },
                    new QueryArgument<IdGraphType> { Name = "specificationId" },
                    new QueryArgument<IdGraphType> { Name = "manufacturerId" }
                ))
                .ResolveAsync(async context =>
                {
                    var nodeContainerId = context.GetArgument<Guid>("nodeContainerId");

                    var correlationId = Guid.NewGuid();

                    var userContext = context.UserContext as GraphQLUserContext;
                    var userName = userContext.Username;

                    // Get the users current work task (will fail, if user has not selected a work task)
                    var currentWorkTaskIdResult = WorkQueryHelper.GetUserCurrentWorkId(userName, queryDispatcher);

                    if (currentWorkTaskIdResult.IsFailed)
                        return new CommandResult(currentWorkTaskIdResult);

                    var commandUserContext = new UserContext(userName, currentWorkTaskIdResult.Value);

                    var updateCmd = new UpdateNodeContainerProperties(correlationId, commandUserContext, nodeContainerId)
                    {
                        SpecificationId = context.HasArgument("specificationId") ? context.GetArgument<Guid>("specificationId") : null,
                        ManufacturerId = context.HasArgument("manufacturerId") ? context.GetArgument<Guid>("manufacturerId") : null,
                    };

                    var updateResult = await commandDispatcher.HandleAsync<UpdateNodeContainerProperties, Result>(updateCmd);

                    return new CommandResult(updateResult);
                });

            Field<CommandResultType>("remove")
                .Description("Remove node container from the route network node")
                .Arguments(new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "nodeContainerId" }
                ))
                .ResolveAsync(async context =>
                {
                    var nodeContainerId = context.GetArgument<Guid>("nodeContainerId");

                    var correlationId = Guid.NewGuid();

                    var userContext = context.UserContext as GraphQLUserContext;
                    var userName = userContext.Username;

                    // Get the users current work task (will fail, if user has not selected a work task)
                    var currentWorkTaskIdResult = WorkQueryHelper.GetUserCurrentWorkId(userName, queryDispatcher);

                    if (currentWorkTaskIdResult.IsFailed)
                        return new CommandResult(currentWorkTaskIdResult);

                    var commandUserContext = new UserContext(userName, currentWorkTaskIdResult.Value);

                    var removeNodeContainer = new RemoveNodeContainerFromRouteNetwork(
                      correlationId: correlationId,
                      userContext: commandUserContext,
                      nodeContainerId: nodeContainerId
                    );

                    var removeResult = await commandDispatcher.HandleAsync<RemoveNodeContainerFromRouteNetwork, Result>(removeNodeContainer);

                    return new CommandResult(removeResult);
                });


            Field<CommandResultType>("placeRackInNodeContainer")
                .Description("Place a rack in the node container")
                .Arguments(new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "nodeContainerId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "rackId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "rackSpecificationId" },
                    new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "rackName" },
                    new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "rackHeightInUnits" }
              ))
                .ResolveAsync(async context =>
                {
                    var nodeContainerId = context.GetArgument<Guid>("nodeContainerId");
                    var rackId = context.GetArgument<Guid>("rackId");
                    var rackSpecificationId = context.GetArgument<Guid>("rackSpecificationId");
                    var rackName = context.GetArgument<string>("rackName");
                    var rackHeightInUnits = context.GetArgument<int>("rackHeightInUnits");

                    var correlationId = Guid.NewGuid();

                    var userContext = context.UserContext as GraphQLUserContext;
                    var userName = userContext.Username;

                    // Get the users current work task (will fail, if user has not selected a work task)
                    var currentWorkTaskIdResult = WorkQueryHelper.GetUserCurrentWorkId(userName, queryDispatcher);

                    if (currentWorkTaskIdResult.IsFailed)
                        return new CommandResult(currentWorkTaskIdResult);

                    var commandUserContext = new UserContext(userName, currentWorkTaskIdResult.Value);

                    var placeRackInNodeContainer = new PlaceRackInNodeContainer(
                      correlationId: correlationId,
                      userContext: commandUserContext,
                      nodeContainerId: nodeContainerId,
                      rackSpecificationId: rackSpecificationId,
                      rackId: Guid.NewGuid(),
                      rackName: rackName,
                      rackHeightInUnits: rackHeightInUnits
                    );

                    var removeResult = await commandDispatcher.HandleAsync<PlaceRackInNodeContainer, Result>(placeRackInNodeContainer);

                    return new CommandResult(removeResult);
                });

            Field<CommandResultType>("removeRackFromNodeContainer")
              .Description("Remove a rack from the node container")
              .Arguments(new QueryArguments(
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "rackId" }
              ))
              .ResolveAsync(async context =>
              {
                  var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                  var rackId = context.GetArgument<Guid>("rackId");

                  var correlationId = Guid.NewGuid();

                  var userContext = context.UserContext as GraphQLUserContext;
                  var userName = userContext.Username;

                  // Get the users current work task (will fail, if user has not selected a work task)
                  var currentWorkTaskIdResult = WorkQueryHelper.GetUserCurrentWorkId(userName, queryDispatcher);

                  if (currentWorkTaskIdResult.IsFailed)
                      return new CommandResult(currentWorkTaskIdResult);

                  var commandUserContext = new UserContext(userName, currentWorkTaskIdResult.Value);

                  var removeRackFromNodeContainer = new RemoveRackFromNodeContainer(
                    correlationId: correlationId,
                    userContext: commandUserContext,
                    routeNodeId: routeNodeId,
                    rackId: rackId
                  );

                  var removeResult = await commandDispatcher.HandleAsync<RemoveRackFromNodeContainer, Result>(removeRackFromNodeContainer);

                  return new CommandResult(removeResult);
              });


            Field<CommandResultType>("placeTerminalEquipmentInNodeContainer")
                .Description("Place a terminal directly in a node container or in a node container rack")
                .Arguments(new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalEquipmentSpecificationId" },
                    new QueryArgument<IdGraphType> { Name = "manufacturerId" },
                    new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "numberOfEquipments" },
                    new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "startSequenceNumber" },
                    new QueryArgument<NonNullGraphType<TerminalEquipmentNamingMethodEnumType>> { Name = "terminalEquipmentNamingMethod" },
                    new QueryArgument<NamingInfoInputType> { Name = "namingInfo" },
                    new QueryArgument<SubrackPlacementInfoInputType> { Name = "subrackPlacementInfo" },
                    new QueryArgument<AddressInfoInputType> { Name = "addressInfo" }
                ))
                .ResolveAsync(async context =>
                {
                    var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                    var terminalEquipmentSpecificationId = context.GetArgument<Guid>("terminalEquipmentSpecificationId");
                    var manufacturerId = context.GetArgument<Guid?>("manufacturerId");
                    var numberOfEquipments = context.GetArgument<int>("numberOfEquipments");
                    var startSequenceNumber = context.GetArgument<int>("startSequenceNumber");
                    var terminalEquipmentNamingMethod = context.GetArgument<TerminalEquipmentNamingMethodEnum>("terminalEquipmentNamingMethod");
                    var namingInfo = context.GetArgument<NamingInfo>("namingInfo");
                    var subrackPlacementInfo = context.GetArgument<SubrackPlacementInfo>("subrackPlacementInfo");
                    var addressInfo = context.GetArgument<AddressInfo>("addressInfo");

                    var getNodeContainerResult = QueryHelper.GetNodeContainerFromRouteNodeId(queryDispatcher, routeNodeId);

                    if (getNodeContainerResult.IsFailed)
                    {
                        foreach (var error in getNodeContainerResult.Errors)
                            context.Errors.Add(new ExecutionError(error.Message));

                        return null;
                    }

                    var nodeContainer = getNodeContainerResult.Value;

                    var correlationId = Guid.NewGuid();

                    var userContext = context.UserContext as GraphQLUserContext;
                    var userName = userContext.Username;

                    // Get the users current work task (will fail, if user has not selected a work task)
                    var currentWorkTaskIdResult = WorkQueryHelper.GetUserCurrentWorkId(userName, queryDispatcher);

                    if (currentWorkTaskIdResult.IsFailed)
                        return new CommandResult(currentWorkTaskIdResult);

                    var commandUserContext = new UserContext(userName, currentWorkTaskIdResult.Value);

                    var placeEquipmentInNodeContainer = new PlaceTerminalEquipmentInNodeContainer(
                      correlationId: correlationId,
                      userContext: commandUserContext,
                      nodeContainerId: nodeContainer.Id,
                      terminalEquipmentSpecificationId: terminalEquipmentSpecificationId,
                      terminalEquipmentId: Guid.NewGuid(),
                      numberOfEquipments: numberOfEquipments,
                      startSequenceNumber: startSequenceNumber,
                      namingMethod: terminalEquipmentNamingMethod,
                      namingInfo: namingInfo
                    )
                    {
                        AddressInfo = addressInfo,
                        SubrackPlacementInfo = subrackPlacementInfo,
                        ManufacturerId = manufacturerId
                    };

                    var removeResult = await commandDispatcher.HandleAsync<PlaceTerminalEquipmentInNodeContainer, Result>(placeEquipmentInNodeContainer);

                    return new CommandResult(removeResult);
                });

            Field<CommandResultType>("updateRackProperties")
               .Description("Mutation that can be used to change the properties of a rack inside a node container")
               .Arguments(new QueryArguments(
                   new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                   new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "rackId" },
                   new QueryArgument<IdGraphType> { Name = "specificationId" },
                   new QueryArgument<StringGraphType> { Name = "name" },
                   new QueryArgument<IntGraphType> { Name = "heightInUnits" }
               ))
               .ResolveAsync(async context =>
               {
                   var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                   var rackId = context.GetArgument<Guid>("rackId");

                   var correlationId = Guid.NewGuid();

                   var userContext = context.UserContext as GraphQLUserContext;
                   var userName = userContext.Username;

                   // Get the users current work task (will fail, if user has not selected a work task)
                   var currentWorkTaskIdResult = WorkQueryHelper.GetUserCurrentWorkId(userName, queryDispatcher);

                   if (currentWorkTaskIdResult.IsFailed)
                       return new CommandResult(currentWorkTaskIdResult);

                   var commandUserContext = new UserContext(userName, currentWorkTaskIdResult.Value);

                   var updateCmd = new UpdateRackProperties(correlationId, commandUserContext, routeNodeId, rackId)
                   {
                       SpecificationId = context.HasArgument("specificationId") ? context.GetArgument<Guid>("specificationId") : null,
                       Name = context.HasArgument("name") ? context.GetArgument<string>("name") : null,
                       HeightInUnits = context.HasArgument("heightInUnits") ? context.GetArgument<int>("heightInUnits") : null,
                   };

                   var updateResult = await commandDispatcher.HandleAsync<UpdateRackProperties, Result>(updateCmd);

                   return new CommandResult(updateResult);
               });

            Field<CommandResultType>("moveRackEquipment")
                .Description("Mutation that moves a terminal equipment within or between racks")
                .Arguments(new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalEquipmentId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "moveToRackId" },
                    new QueryArgument<IntGraphType> { Name = "moveToRackPosition" }
                ))
                .ResolveAsync(async context =>
                {
                    var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                    var terminalEquipmentId = context.GetArgument<Guid>("terminalEquipmentId");
                    var moveToRackId = context.GetArgument<Guid>("moveToRackId");
                    var moveToRackPosition = context.GetArgument<int>("moveToRackPosition");

                    var getNodeContainerResult = QueryHelper.GetNodeContainerFromRouteNodeId(queryDispatcher, routeNodeId);
                    if (getNodeContainerResult.IsFailed)
                    {
                        foreach (var error in getNodeContainerResult.Errors)
                            context.Errors.Add(new ExecutionError(error.Message));

                        return null;
                    }

                    var correlationId = Guid.NewGuid();
                    var userContext = context.UserContext as GraphQLUserContext;
                    var userName = userContext.Username;

                    // Get the users current work task (will fail, if user has not selected a work task)
                    var currentWorkTaskIdResult = WorkQueryHelper.GetUserCurrentWorkId(userName, queryDispatcher);

                    if (currentWorkTaskIdResult.IsFailed)
                        return new CommandResult(currentWorkTaskIdResult);

                    var commandUserContext = new UserContext(userName, currentWorkTaskIdResult.Value);

                    var cmd = new MoveRackEquipmentInNodeContainer(correlationId, commandUserContext, getNodeContainerResult.Value.Id, terminalEquipmentId, moveToRackId, moveToRackPosition);

                    var cmdResult = await commandDispatcher.HandleAsync<MoveRackEquipmentInNodeContainer, Result>(cmd);

                    return new CommandResult(cmdResult);
                });

            Field<CommandResultType>("arrangeRackEquipment")
                .Description("Mutation that can move terminal equipments up/down in a rack")
                .Arguments(new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalEquipmentId" },
                    new QueryArgument<NonNullGraphType<RackEquipmentArrangeMethodEnumType>> { Name = "arrangeMethod" },
                    new QueryArgument<IntGraphType> { Name = "numberOfRackPositions" }
                ))
                .ResolveAsync(async context =>
                {
                    var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                    var terminalEquipmentId = context.GetArgument<Guid>("terminalEquipmentId");
                    var arrangeMethod = context.GetArgument<RackEquipmentArrangeMethodEnum>("arrangeMethod");
                    var numberOfRackPositions = context.GetArgument<int>("numberOfRackPositions");

                    var getNodeContainerResult = QueryHelper.GetNodeContainerFromRouteNodeId(queryDispatcher, routeNodeId);
                    if (getNodeContainerResult.IsFailed)
                    {
                        foreach (var error in getNodeContainerResult.Errors)
                            context.Errors.Add(new ExecutionError(error.Message));

                        return null;
                    }

                    var correlationId = Guid.NewGuid();
                    var userContext = context.UserContext as GraphQLUserContext;
                    var userName = userContext.Username;

                    // Get the users current work task (will fail, if user has not selected a work task)
                    var currentWorkTaskIdResult = WorkQueryHelper.GetUserCurrentWorkId(userName, queryDispatcher);

                    if (currentWorkTaskIdResult.IsFailed)
                        return new CommandResult(currentWorkTaskIdResult);

                    var commandUserContext = new UserContext(userName, currentWorkTaskIdResult.Value);

                    var cmd = new ArrangeRackEquipmentInNodeContainer(correlationId, commandUserContext, getNodeContainerResult.Value.Id, terminalEquipmentId, arrangeMethod, numberOfRackPositions);

                    var cmdResult = await commandDispatcher.HandleAsync<ArrangeRackEquipmentInNodeContainer, Result>(cmd);

                    return new CommandResult(cmdResult);
                });
        }
    }
}
