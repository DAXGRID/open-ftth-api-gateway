using FluentResults;
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

namespace OpenFTTH.APIGateway.GraphQL.RouteNetwork.Mutations
{
    public class NodeContainerMutations : ObjectGraphType
    {
        public NodeContainerMutations(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IEventStore eventStore)
        {
            Description = "Node container mutations";

            Field<CommandResultType>(
              "placeNodeContainerInRouteNetwork",
              description: "Place a node container (i.e. conduit closure, well, cabinet whatwever) in a route network node",
              arguments: new QueryArguments(
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "nodeContainerId" },
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "nodeContainerSpecificationId" },
                  new QueryArgument<IdGraphType> { Name = "manufacturerId" }
              ),
              resolve: context =>
              {
                  var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                  var nodeContainerId = context.GetArgument<Guid>("nodeContainerId");
                  var nodeContainerSpecificationId = context.GetArgument<Guid>("nodeContainerSpecificationId");
                  var manufacturerId = context.GetArgument<Guid>("manufacturerId");

                  var userContext = context.UserContext as GraphQLUserContext;
                  var userName = userContext.Username;

                  Guid correlationId = Guid.NewGuid();

                  // TODO: Get from work manager
                  var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");

                  var commandUserContext = new UserContext(userName, workTaskId)
                  {
                      EditingRouteNodeId = routeNodeId
                  };


                  // First register the walk in the route network where the client want to place the node container
                  var nodeOfInterestId = Guid.NewGuid();
                  var walk = new RouteNetworkElementIdList();
                  var registerNodeOfInterestCommand = new RegisterNodeOfInterest(correlationId, commandUserContext, nodeOfInterestId, routeNodeId);

                  var registerNodeOfInterestCommandResult = commandDispatcher.HandleAsync<RegisterNodeOfInterest, Result<RouteNetworkInterest>>(registerNodeOfInterestCommand).Result;

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

                  var placeNodeContainerResult = commandDispatcher.HandleAsync<PlaceNodeContainerInRouteNetwork, Result>(placeNodeContainerCommand).Result;

                  // Unregister interest if place node container failed
                  if (placeNodeContainerResult.IsFailed)
                  {
                      var unregisterCommandResult = commandDispatcher.HandleAsync<UnregisterInterest, Result>(new UnregisterInterest(correlationId, commandUserContext, nodeOfInterestId)).Result;

                      if (unregisterCommandResult.IsFailed)
                          return new CommandResult(unregisterCommandResult);
                  }

                  return new CommandResult(placeNodeContainerResult);
              }
            );

            Field<CommandResultType>(
                 "reverseVerticalContentAlignment",
                 description: "Toggle whether the content in the node container should be drawed from bottom up or top down",
                 arguments: new QueryArguments(
                     new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "nodeContainerId" }
                 ),
                 resolve: context =>
                 {
                     var userContext = context.UserContext as GraphQLUserContext;
                     var userName = userContext.Username;

                     Guid correlationId = Guid.NewGuid();

                     // TODO: Get from work manager
                     var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");

                     var commandUserContext = new UserContext(userName, workTaskId);

                     var nodeContainerId = context.GetArgument<Guid>("nodeContainerId");

                     var reverseAlignmentCmd = new ReverseNodeContainerVerticalContentAlignment(correlationId, commandUserContext, nodeContainerId);

                     var reverseAlignmentCmdResult = commandDispatcher.HandleAsync<ReverseNodeContainerVerticalContentAlignment, Result>(reverseAlignmentCmd).Result;

                     return new CommandResult(reverseAlignmentCmdResult);
                 }
           );

            Field<CommandResultType>(
                 "updateProperties",
                 description: "Mutation that can be used to change the node container specification and/or manufacturer",
                 arguments: new QueryArguments(
                     new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "nodeContainerId" },
                     new QueryArgument<IdGraphType> { Name = "specificationId" },
                     new QueryArgument<IdGraphType> { Name = "manufacturerId" }
                 ),
                 resolve: context =>
                 {
                     var nodeContainerId = context.GetArgument<Guid>("nodeContainerId");

                     var correlationId = Guid.NewGuid();

                     var userContext = context.UserContext as GraphQLUserContext;
                     var userName = userContext.Username;

                     // TODO: Get from work manager
                     var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");

                     var commandUserContext = new UserContext(userName, workTaskId);

                     var updateCmd = new UpdateNodeContainerProperties(correlationId, commandUserContext, nodeContainerId)
                     {
                         SpecificationId = context.HasArgument("specificationId") ? context.GetArgument<Guid>("specificationId") : null,
                         ManufacturerId = context.HasArgument("manufacturerId") ? context.GetArgument<Guid>("manufacturerId") : null,
                     };

                     var updateResult = commandDispatcher.HandleAsync<UpdateNodeContainerProperties, Result>(updateCmd).Result;

                     return new CommandResult(updateResult);
                 }
             );


            Field<CommandResultType>(
              "remove",
              description: "Remove node container from the route network node",
              arguments: new QueryArguments(
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "nodeContainerId" }
              ),
              resolve: context =>
              {
                  var nodeContainerId = context.GetArgument<Guid>("nodeContainerId");

                  var correlationId = Guid.NewGuid();

                  var userContext = context.UserContext as GraphQLUserContext;
                  var userName = userContext.Username;

                  // TODO: Get from work manager
                  var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");

                  var commandUserContext = new UserContext(userName, workTaskId);

                  var removeNodeContainer = new RemoveNodeContainerFromRouteNetwork(
                    correlationId: correlationId,
                    userContext: commandUserContext,
                    nodeContainerId: nodeContainerId
                  );

                  var removeResult = commandDispatcher.HandleAsync<RemoveNodeContainerFromRouteNetwork, Result>(removeNodeContainer).Result;

                  return new CommandResult(removeResult);
              }
            );


            Field<CommandResultType>(
              "placeRackInNodeContainer",
              description: "Place a rack in the node container",
              arguments: new QueryArguments(
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "nodeContainerId" },
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "rackId" },
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "rackSpecificationId" },
                  new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "rackName" },
                  new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "rackHeightInUnits" }
              ),
              resolve: context =>
              {
                  var nodeContainerId = context.GetArgument<Guid>("nodeContainerId");
                  var rackId = context.GetArgument<Guid>("rackId");
                  var rackSpecificationId = context.GetArgument<Guid>("rackSpecificationId");
                  var rackName = context.GetArgument<string>("rackName");
                  var rackHeightInUnits = context.GetArgument<int>("rackHeightInUnits");

                  var correlationId = Guid.NewGuid();

                  var userContext = context.UserContext as GraphQLUserContext;
                  var userName = userContext.Username;

                  // TODO: Get from work manager
                  var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");

                  var commandUserContext = new UserContext(userName, workTaskId);

                  var placeRackInNodeContainer = new PlaceRackInNodeContainer(
                    correlationId: correlationId,
                    userContext: commandUserContext,
                    nodeContainerId: nodeContainerId,
                    rackSpecificationId: rackSpecificationId,
                    rackId: Guid.NewGuid(),
                    rackName: rackName,
                    rackHeightInUnits: rackHeightInUnits
                  );

                  var removeResult = commandDispatcher.HandleAsync<PlaceRackInNodeContainer, Result>(placeRackInNodeContainer).Result;

                  return new CommandResult(removeResult);
              }
            );


            Field<CommandResultType>(
             "placeTerminalEquipmentInNodeContainer",
             description: "Place a terminal directly in a node container or in a node container rack",
             arguments: new QueryArguments(
                 new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                 new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalEquipmentSpecificationId" },
                 new QueryArgument<IdGraphType> { Name = "manufacturerId" },
                 new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "numberOfEquipments" },
                 new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "startSequenceNumber" },
                 new QueryArgument<NonNullGraphType<TerminalEquipmentNamingMethodEnumType>> { Name = "terminalEquipmentNamingMethod" },
                 new QueryArgument<NamingInfoInputType> { Name = "namingInfo" },
                 new QueryArgument<SubrackPlacementInfoInputType> { Name = "subrackPlacementInfo" },
                 new QueryArgument<AddressInfoInputType> { Name = "addressInfo" }
             ),
             resolve: context =>
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

                 // TODO: Get from work manager
                 var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");

                 var commandUserContext = new UserContext(userName, workTaskId);

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

                 var removeResult = commandDispatcher.HandleAsync<PlaceTerminalEquipmentInNodeContainer, Result>(placeEquipmentInNodeContainer).Result;

                 return new CommandResult(removeResult);
             }
           );
        }
    }
}
