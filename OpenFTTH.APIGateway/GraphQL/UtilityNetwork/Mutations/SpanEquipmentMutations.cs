using FluentResults;
using GraphQL;
using GraphQL.Types;
using OpenFTTH.APIGateway.CoreTypes;
using OpenFTTH.APIGateway.GraphQL.Addresses.Types;
using OpenFTTH.APIGateway.GraphQL.Core.Model;
using OpenFTTH.APIGateway.GraphQL.RouteNetwork.Types;
using OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using System;
using System.Collections.Generic;

namespace OpenFTTH.APIGateway.GraphQL.RouteNetwork.Mutations
{
    public class SpanEquipmentMutations : ObjectGraphType
    {
        public SpanEquipmentMutations(ICommandDispatcher commandDispatcher, IEventStore eventStore)
        {
            Description = "Span equipment mutations";

            FieldAsync<CommandResultType>(
              "placeSpanEquipmentInRouteNetwork",
              description: "Place a span equipment (i.e. conduit, cable whatwever) in the route network",
              arguments: new QueryArguments(
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "spanEquipmentId" },
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "spanEquipmentSpecificationId" },
                  new QueryArgument<NonNullGraphType<ListGraphType<IdGraphType>>> { Name = "routeSegmentIds" },
                  new QueryArgument<IdGraphType> { Name = "manufacturerId" },
                  new QueryArgument<MarkingInfoInputType> { Name = "markingInfo" },
                  new QueryArgument<NamingInfoInputType> { Name = "namingInfo" },
                  new QueryArgument<AddressInfoInputType> { Name = "addressInfo" }
              ),
              resolve: async context =>
              {
                  var spanEquipmentId = context.GetArgument<Guid>("spanEquipmentId");
                  var spanEquipmentSpecificationId = context.GetArgument<Guid>("spanEquipmentSpecificationId");
                  var routeSegmentIds = context.GetArgument<List<Guid>>("routeSegmentIds");
                  var manufacturerId = context.GetArgument<Guid>("manufacturerId");
                  var markingInfo = context.GetArgument<MarkingInfo>("markingInfo");
                  var namingInfo = context.GetArgument<NamingInfo>("namingInfo");
                  var addressInfo = context.GetArgument<AddressInfo>("addressInfo");

                  // Name conduit span equipment
                  // TODO: Refactor into som class responsible for span equipment naming
                  var spec = eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications[spanEquipmentSpecificationId];
                  namingInfo = CalculateName(eventStore, namingInfo, spec);

                  var correlationId = Guid.NewGuid();

                  var userContext = context.UserContext as GraphQLUserContext;
                  var userName = userContext.Username;

                  // TODO: Get from work manager
                  var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");
                  var commandUserContext = new UserContext(userName, workTaskId);

                  var spanEquipments = eventStore.Projections.Get<UtilityNetworkProjection>().SpanEquipmentsByEquipmentId;

                  // First register the walk in the route network where the client want to place the span equipment
                  var walkOfInterestId = Guid.NewGuid();
                  var walk = new RouteNetworkElementIdList();
                  walk.AddRange(routeSegmentIds);

                  var registerWalkOfInterestCommand = new RegisterWalkOfInterest(correlationId, commandUserContext, walkOfInterestId, walk);

                  var registerWalkOfInterestCommandResult = await commandDispatcher.HandleAsync<RegisterWalkOfInterest, Result<RouteNetworkInterest>>(registerWalkOfInterestCommand);

                  if (registerWalkOfInterestCommandResult.IsFailed)
                  {
                      return new CommandResult(registerWalkOfInterestCommandResult);
                  }

                  // Now place the conduit in the walk
                  var placeSpanEquipmentCommand = new PlaceSpanEquipmentInRouteNetwork(
                      correlationId, commandUserContext, spanEquipmentId, spanEquipmentSpecificationId, registerWalkOfInterestCommandResult.Value)
                  {
                      ManufacturerId = manufacturerId,
                      NamingInfo = namingInfo,
                      MarkingInfo = markingInfo,
                      LifecycleInfo = new LifecycleInfo(DeploymentStateEnum.InService, null, null),
                      AddressInfo = addressInfo
                  };

                  var placeSpanEquipmentResult = await commandDispatcher.HandleAsync<PlaceSpanEquipmentInRouteNetwork, Result>(placeSpanEquipmentCommand);

                  // Unregister interest if place span equipment failed
                  if (placeSpanEquipmentResult.IsFailed)
                  {
                      var unregisterCommandResult = await commandDispatcher.HandleAsync<UnregisterInterest, Result>(
                          new UnregisterInterest(correlationId, commandUserContext, walkOfInterestId));

                      if (unregisterCommandResult.IsFailed)
                          return new CommandResult(unregisterCommandResult);
                  }

                  return new CommandResult(placeSpanEquipmentResult);
              }
            );

            FieldAsync<CommandResultType>(
              "affixSpanEquipmentToNodeContainer",
              description: "Affix a span equipment to a node container - i.e. to some condult closure, man hole etc.",
              arguments: new QueryArguments(
                  new QueryArgument<NonNullGraphType<ListGraphType<IdGraphType>>> { Name = "spanSegmentIds" },
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "nodeContainerId" },
                  new QueryArgument<NonNullGraphType<NodeContainerSideEnumType>> { Name = "nodeContainerSide" }
              ),
              resolve: async context =>
              {
                  var spanSegmentIds = context.GetArgument<List<Guid>>("spanSegmentIds");
                  var nodeContainerId = context.GetArgument<Guid>("nodeContainerId");
                  var side = context.GetArgument<NodeContainerSideEnum>("nodeContainerSide");

                  var correlationId = Guid.NewGuid();

                  var userContext = context.UserContext as GraphQLUserContext;
                  var userName = userContext.Username;

                  // TODO: Get from work manager
                  var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");

                  var commandUserContext = new UserContext(userName, workTaskId);

                  foreach (var spanSegmentId in spanSegmentIds)
                  {
                      var affixCommand = new AffixSpanEquipmentToNodeContainer(correlationId, commandUserContext, spanSegmentId, nodeContainerId, side);

                      var affixCommandResult = await commandDispatcher.HandleAsync<AffixSpanEquipmentToNodeContainer, Result>(affixCommand);

                      if (affixCommandResult.IsFailed)
                          return new CommandResult(affixCommandResult);
                  }

                  return new CommandResult(Result.Ok());
              }
            );

            FieldAsync<CommandResultType>(
              "detachSpanEquipmentFromNodeContainer",
              description: "Detach a span equipment from a node container - i.e. from some condult closure, man hole etc.",
              arguments: new QueryArguments(
                  new QueryArgument<ListGraphType<IdGraphType>> { Name = "spanSegmentIds" },
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" }
              ),
              resolve: async context =>
              {
                  var spanSegmentIds = context.GetArgument<List<Guid>>("spanSegmentIds");
                  var routeNodeId = context.GetArgument<Guid>("routeNodeId");

                  var correlationId = Guid.NewGuid();

                  var userContext = context.UserContext as GraphQLUserContext;
                  var userName = userContext.Username;

                  // TODO: Get from work manager
                  var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");

                  var commandUserContext = new UserContext(userName, workTaskId)
                  {
                      EditingRouteNodeId = routeNodeId
                  };

                  foreach (var spanSegmentId in spanSegmentIds)
                  {
                      var detachCommand = new DetachSpanEquipmentFromNodeContainer(correlationId, commandUserContext, spanSegmentId, routeNodeId);
                      var detachCommandResult = await commandDispatcher.HandleAsync<DetachSpanEquipmentFromNodeContainer, Result>(detachCommand);

                      if (detachCommandResult.IsFailed)
                          return new CommandResult(detachCommandResult);
                  }

                  return new CommandResult(Result.Ok());
              }
            );

            FieldAsync<CommandResultType>(
              "cutSpanSegments",
              description: "Cut the span segments belonging to som span equipment at the route node specified",
              arguments: new QueryArguments(
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                  new QueryArgument<NonNullGraphType<ListGraphType<IdGraphType>>> { Name = "spanSegmentstoCut" }
              ),
              resolve: async context =>
              {
                  var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                  var spanSegmentToCut = context.GetArgument<Guid[]>("spanSegmentstoCut");

                  var correlationId = Guid.NewGuid();

                  var userContext = context.UserContext as GraphQLUserContext;
                  var userName = userContext.Username;

                  // TODO: Get from work manager
                  var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");

                  var commandUserContext = new UserContext(userName, workTaskId)
                  {
                      EditingRouteNodeId = routeNodeId
                  };

                  var cutCmd = new CutSpanSegmentsAtRouteNode(correlationId, commandUserContext, routeNodeId, spanSegmentToCut);
                  var cutResult = await commandDispatcher.HandleAsync<CutSpanSegmentsAtRouteNode, Result>(cutCmd);

                  return new CommandResult(cutResult);
              }
            );

            FieldAsync<CommandResultType>(
              "connectSpanSegments",
              description: "Connect the span segments belonging to two different span equipment at the route node specified",
              arguments: new QueryArguments(
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                  new QueryArgument<NonNullGraphType<ListGraphType<IdGraphType>>> { Name = "spanSegmentsToConnect" }
              ),
              resolve: async context =>
              {
                  var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                  var spanSegmentToConnect = context.GetArgument<Guid[]>("spanSegmentsToConnect");

                  var correlationId = Guid.NewGuid();

                  var userContext = context.UserContext as GraphQLUserContext;
                  var userName = userContext.Username;

                  // TODO: Get from work manager
                  var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");

                  var commandUserContext = new UserContext(userName, workTaskId)
                  {
                      EditingRouteNodeId = routeNodeId
                  };

                  var connectCmd = new ConnectSpanSegmentsAtRouteNode(correlationId, commandUserContext, routeNodeId, spanSegmentToConnect);
                  var connectResult = await commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd);

                  return new CommandResult(connectResult);
              }
            );

            FieldAsync<CommandResultType>(
              "disconnectSpanSegments",
              description: "Disconnect two span segments belonging to two different span equipment at the route node specified",
              arguments: new QueryArguments(
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                  new QueryArgument<NonNullGraphType<ListGraphType<IdGraphType>>> { Name = "spanSegmentsToDisconnect" }
              ),
              resolve: async context =>
              {
                  var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                  var spanSegmentToDisconnect = context.GetArgument<Guid[]>("spanSegmentsToDisconnect");

                  var correlationId = Guid.NewGuid();

                  var userContext = context.UserContext as GraphQLUserContext;
                  var userName = userContext.Username;

                  // TODO: Get from work manager
                  var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");

                  var commandUserContext = new UserContext(userName, workTaskId)
                  {
                      EditingRouteNodeId = routeNodeId
                  };

                  var disconnectCmd = new DisconnectSpanSegmentsAtRouteNode(correlationId, commandUserContext, routeNodeId, spanSegmentToDisconnect);
                  var disconnectResult = await commandDispatcher.HandleAsync<DisconnectSpanSegmentsAtRouteNode, Result>(disconnectCmd);

                  return new CommandResult(disconnectResult);
              }
            );

            FieldAsync<CommandResultType>(
              "addAdditionalInnerSpanStructures",
              description: "Add inner span structures to an existing span equipment",
              arguments: new QueryArguments(
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "spanEquipmentOrSegmentId" },
                  new QueryArgument<NonNullGraphType<ListGraphType<IdGraphType>>> { Name = "spanStructureSpecificationIds" }
              ),
              resolve: async context =>
              {
                  var spanEquipmentOrSegmentId = context.GetArgument<Guid>("spanEquipmentOrSegmentId");
                  var specificationsId = context.GetArgument<Guid[]>("spanStructureSpecificationIds");

                  var correlationId = Guid.NewGuid();

                  var userContext = context.UserContext as GraphQLUserContext;
                  var userName = userContext.Username;

                  // TODO: Get from work manager
                  var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");

                  var commandUserContext = new UserContext(userName, workTaskId);

                  var addStructure = new PlaceAdditionalStructuresInSpanEquipment(
                    correlationId: correlationId,
                    userContext: commandUserContext,
                    spanEquipmentId: spanEquipmentOrSegmentId,
                    structureSpecificationIds: specificationsId
                  );

                  var addStructureResult = await commandDispatcher.HandleAsync<PlaceAdditionalStructuresInSpanEquipment, Result>(addStructure);

                  return new CommandResult(addStructureResult);
              }
            );

            FieldAsync<CommandResultType>(
              "removeSpanStructure",
              description: "Remove inner or outer span structure of a span equipment. When the outer span structure is removed the entire span equipment is removed from the network.",
              arguments: new QueryArguments(
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "spanSegmentId" }
              ),
              resolve: async context =>
              {
                  var spanSegmentId = context.GetArgument<Guid>("spanSegmentId");

                  var correlationId = Guid.NewGuid();

                  var userContext = context.UserContext as GraphQLUserContext;
                  var userName = userContext.Username;

                  // TODO: Get from work manager
                  var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");

                  var commandUserContext = new UserContext(userName, workTaskId);

                  var removeStructure = new RemoveSpanStructureFromSpanEquipment(
                    correlationId: correlationId,
                    userContext: commandUserContext,
                    spanSegmentId: spanSegmentId
                  );

                  var removeStructureResult = await commandDispatcher.HandleAsync<RemoveSpanStructureFromSpanEquipment, Result>(removeStructure);

                  return new CommandResult(removeStructureResult);
              }
            );

            FieldAsync<CommandResultType>(
              "move",
              description: "Move a span equipment / change its walk in the route network",
              arguments: new QueryArguments(
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "spanEquipmentOrSegmentId" },
                  new QueryArgument<NonNullGraphType<ListGraphType<IdGraphType>>> { Name = "routeSegmentIds" }
              ),
              resolve: async context =>
              {
                  var spanEquipmentOrSegmentId = context.GetArgument<Guid>("spanEquipmentOrSegmentId");

                  var correlationId = Guid.NewGuid();

                  var userContext = context.UserContext as GraphQLUserContext;
                  var userName = userContext.Username;

                  // TODO: Get from work manager
                  var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");

                  var commandUserContext = new UserContext(userName, workTaskId);

                  Guid[] routeSegmentIds = context.GetArgument<Guid[]>("routeSegmentIds");

                  RouteNetworkElementIdList newWalkIds = new();
                  newWalkIds.AddRange(routeSegmentIds);

                  var moveCmd = new MoveSpanEquipment(
                    correlationId: correlationId,
                    userContext: commandUserContext,
                    spanEquipmentId: spanEquipmentOrSegmentId,
                    newWalkIds: newWalkIds
                  );

                  var moveCmdResult = await commandDispatcher.HandleAsync<MoveSpanEquipment, Result>(moveCmd);

                  return new CommandResult(moveCmdResult);
              }
            );

            FieldAsync<CommandResultType>(
             "updateProperties",
             description: "Mutation that can be used to change the span equipment specification, manufacturer and/or marking information",
             arguments: new QueryArguments(
                 new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "spanEquipmentOrSegmentId" },
                 new QueryArgument<IdGraphType> { Name = "spanEquipmentSpecificationId" },
                 new QueryArgument<IdGraphType> { Name = "manufacturerId" },
                 new QueryArgument<MarkingInfoInputType> { Name = "markingInfo" },
                 new QueryArgument<AddressInfoInputType> { Name = "addressInfo" }
             ),
             resolve: async context =>
             {
                 var spanEquipmentOrSegmentId = context.GetArgument<Guid>("spanEquipmentOrSegmentId");

                 var correlationId = Guid.NewGuid();

                 var userContext = context.UserContext as GraphQLUserContext;
                 var userName = userContext.Username;

                 // TODO: Get from work manager
                 var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");

                 var commandUserContext = new UserContext(userName, workTaskId);

                 var updateCmd = new UpdateSpanEquipmentProperties(correlationId, commandUserContext, spanEquipmentOrSegmentId: spanEquipmentOrSegmentId)
                 {
                     SpecificationId = context.HasArgument("spanEquipmentSpecificationId") ? context.GetArgument<Guid>("spanEquipmentSpecificationId") : null,
                     ManufacturerId = context.HasArgument("manufacturerId") ? context.GetArgument<Guid>("manufacturerId") : null,
                     MarkingInfo = context.HasArgument("markingInfo") ? context.GetArgument<MarkingInfo>("markingInfo") : null,
                     AddressInfo = context.HasArgument("addressInfo") ? context.GetArgument<AddressInfo>("addressInfo") : null
                 };

                 var updateResult = await commandDispatcher.HandleAsync<UpdateSpanEquipmentProperties, Result>(updateCmd);

                 return new CommandResult(updateResult);
             }
           );

            FieldAsync<CommandResultType>(
             "affixSpanEquipmentToParent",
             description: "Affix a span equipment to a parent span equipment - i.e. put a cable inside a conduit",
             arguments: new QueryArguments(
                 new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                 new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "spanSegmentId1" },
                 new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "spanSegmentId2" }
             ),
             resolve: async context =>
             {
                 var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                 var spanSegmentId1 = context.GetArgument<Guid>("spanSegmentId1");
                 var spanSegmentId2 = context.GetArgument<Guid>("spanSegmentId2");

                 var correlationId = Guid.NewGuid();

                 var userContext = context.UserContext as GraphQLUserContext;
                 var userName = userContext.Username;

                 // TODO: Get from work manager
                 var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");

                 var commandUserContext = new UserContext(userName, workTaskId);

                 var affixCommand = new AffixSpanEquipmentToParent(correlationId, commandUserContext, routeNodeId, spanSegmentId1, spanSegmentId2);
                 var affixCommandResult = await commandDispatcher.HandleAsync<AffixSpanEquipmentToParent, Result>(affixCommand);

                 if (affixCommandResult.IsFailed)
                     return new CommandResult(affixCommandResult);

                 return new CommandResult(Result.Ok());
             }
           );

            FieldAsync<CommandResultType>(
                "connectToTerminalEquipment",
                description: "Connect one or more span segments inside a span equipment to terminals inside a terminal equipmment",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                    new QueryArgument<NonNullGraphType<ListGraphType<ConnectSpanSegmentToTerminalOperationInputType>>> { Name = "connects" }
                ),
                resolve: async context =>
                {
                    var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                    var connects = context.GetArgument<ConnectSpanSegmentToTerminalOperation[]>("connects");

                    var correlationId = Guid.NewGuid();

                    var userContext = context.UserContext as GraphQLUserContext;
                    var userName = userContext.Username;

                    // TODO: Get from work manager
                    var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");

                    var commandUserContext = new UserContext(userName, workTaskId);

                    var connectCommand = new ConnectSpanSegmentsWithTerminalsAtRouteNode(correlationId, commandUserContext, routeNodeId, connects);
                    var connectCommandResult = await commandDispatcher.HandleAsync<ConnectSpanSegmentsWithTerminalsAtRouteNode, Result>(connectCommand);

                    if (connectCommandResult.IsFailed)
                        return new CommandResult(connectCommandResult);

                    return new CommandResult(Result.Ok());
                }
             );

            FieldAsync<CommandResultType>(
               "disconnectFromTerminalEquipment",
               description: "Disconnect one or more span segments inside a span equipment from terminals inside a terminal equipmment",
               arguments: new QueryArguments(
                   new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                   new QueryArgument<NonNullGraphType<ListGraphType<DisconnectSpanSegmentFromTerminalOperationInputType>>> { Name = "disconnects" }
               ),
               resolve: async context =>
               {
                   var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                   var connects = context.GetArgument<DisconnectSpanSegmentFromTerminalOperation[]>("disconnects");

                   var correlationId = Guid.NewGuid();

                   var userContext = context.UserContext as GraphQLUserContext;
                   var userName = userContext.Username;

                   // TODO: Get from work manager
                   var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");

                   var commandUserContext = new UserContext(userName, workTaskId);
                   var connectCommand = new DisconnectSpanSegmentsFromTerminalsAtRouteNode(correlationId, commandUserContext, routeNodeId, connects);

                   var connectCommandResult = await commandDispatcher.HandleAsync<DisconnectSpanSegmentsFromTerminalsAtRouteNode, Result>(connectCommand);

                   if (connectCommandResult.IsFailed)
                       return new CommandResult(connectCommandResult);

                   return new CommandResult(Result.Ok());
               }
            );
        }

        private static NamingInfo CalculateName(IEventStore eventStore, NamingInfo namingInfo, SpanEquipmentSpecification spec)
        {
            if (spec.Category != null && spec.Category.ToLower().Contains("conduit"))
            {
                var nextConduitSeqStr = eventStore.Sequences.GetNextVal("conduit").ToString();

                var conduitName = "R" + nextConduitSeqStr.PadLeft(6, '0');
                namingInfo = new NamingInfo(conduitName, null);
            }
            else if (spec.Category != null && spec.Category.ToLower().Contains("cable"))
            {
                var nextCableSeqStr = eventStore.Sequences.GetNextVal("cable").ToString();

                var conduitName = "K" + nextCableSeqStr.PadLeft(6, '0');
                namingInfo = new NamingInfo(conduitName, null);
            }

            return namingInfo;
        }
    }
}
