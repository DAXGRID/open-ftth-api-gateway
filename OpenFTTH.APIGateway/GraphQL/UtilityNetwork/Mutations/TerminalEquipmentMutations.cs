﻿using FluentResults;
using GraphQL;
using GraphQL.Types;
using OpenFTTH.APIGateway.CoreTypes;
using OpenFTTH.APIGateway.GraphQL.Addresses.Types;
using OpenFTTH.APIGateway.GraphQL.Core.Model;
using OpenFTTH.APIGateway.GraphQL.RouteNetwork.Types;
using OpenFTTH.CQRS;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.EventSourcing;
using OpenFTTH.UtilityGraphService.API.Commands;
using System;

namespace OpenFTTH.APIGateway.GraphQL.RouteNetwork.Mutations
{
    public class TerminalEquipmentMutations : ObjectGraphType
    {
        public TerminalEquipmentMutations(ICommandDispatcher commandDispatcher, IEventStore eventStore)
        {
            Description = "Terminal equipment mutations";

            Field<CommandResultType>(
             "updateProperties",
             description: "Mutation that can be used to change the terminal equipment specification,naming information",
             arguments: new QueryArguments(
                 new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalEquipmentId" },
                 new QueryArgument<IdGraphType> { Name = "terminalEquipmentSpecificationId" },
                 new QueryArgument<IdGraphType> { Name = "manufacturerId" },
                 new QueryArgument<NamingInfoInputType> { Name = "namingInfo" },
                 new QueryArgument<AddressInfoInputType> { Name = "addressInfo" },
                 new QueryArgument<IdGraphType> { Name = "rackId" },
                 new QueryArgument<IntGraphType> { Name = "rackStartUnitPosition" }
             ),
             resolve: context =>
             {
                 var terminalEquipmentId = context.GetArgument<Guid>("terminalEquipmentId");

                 var correlationId = Guid.NewGuid();

                 var userContext = context.UserContext as GraphQLUserContext;
                 var userName = userContext.Username;

                 // TODO: Get from work manager
                 var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");

                 var commandUserContext = new UserContext(userName, workTaskId);


                 var updateCmd = new UpdateTerminalEquipmentProperties(correlationId, commandUserContext, terminalEquipmentId: terminalEquipmentId)
                 {
                     SpecificationId = context.HasArgument("terminalEquipmentSpecificationId") ? context.GetArgument<Guid>("terminalEquipmentSpecificationId") : null,
                     ManufacturerId = context.HasArgument("manufacturerId") ? context.GetArgument<Guid>("manufacturerId") : null,
                     NamingInfo = context.HasArgument("namingInfo") ? context.GetArgument<NamingInfo>("namingInfo") : null,
                     AddressInfo = context.HasArgument("addressInfo") ? context.GetArgument<AddressInfo>("addressInfo") : null,
                     RackId = context.HasArgument("rackId") ? context.GetArgument<Guid>("rackId") : null,
                     StartUnitPosition = context.HasArgument("rackStartUnitPosition") ? context.GetArgument<int>("rackStartUnitPosition") : null,
                 };

                 var updateResult = commandDispatcher.HandleAsync<UpdateTerminalEquipmentProperties, Result>(updateCmd).Result;

                 return new CommandResult(updateResult);
             }
           );

            FieldAsync<CommandResultType>(
             "remove",
             description: "Remove the terminal equipment",
             arguments: new QueryArguments(
                 new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                 new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalEquipmentId" }
             ),
             resolve: async context =>
             {
                 var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                 var terminalEquipmentId = context.GetArgument<Guid>("terminalEquipmentId");

                 var correlationId = Guid.NewGuid();

                 var userContext = context.UserContext as GraphQLUserContext;
                 var userName = userContext.Username;

                 // TODO: Get from work manager
                 var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");

                 var commandUserContext = new UserContext(userName, workTaskId);

                 var removeTerminalEquipment = new RemoveTerminalEquipment(
                   correlationId: correlationId,
                   userContext: commandUserContext,
                   terminalEquipmentId: terminalEquipmentId
                 );

                 var removeResult = await commandDispatcher.HandleAsync<RemoveTerminalEquipment, Result>(removeTerminalEquipment);

                 return new CommandResult(removeResult);
             }
           );

            FieldAsync<CommandResultType>(
               "connectTerminals",
               description: "Connect terminals - i.e. create a patch from one terminal equipment to another",
               arguments: new QueryArguments(
                   new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                   new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "fromTerminalId" },
                   new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "toTerminalId" },
                   new QueryArgument<NonNullGraphType<FloatGraphType>> { Name = "fiberCoordLength" }
               ),
               resolve: async context =>
               {
                   var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                   var fromTerminalId = context.GetArgument<Guid>("fromTerminalId");
                   var toTerminalId = context.GetArgument<Guid>("toTerminalId");
                   var fiberCoordLength = context.GetArgument<Double>("fiberCoordLength");

                   var correlationId = Guid.NewGuid();

                   var userContext = context.UserContext as GraphQLUserContext;
                   var userName = userContext.Username;

                    // TODO: Get from work manager
                    var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");

                   var commandUserContext = new UserContext(userName, workTaskId);

                   var connectCommand = new ConnectTerminalsAtRouteNode(correlationId, commandUserContext, routeNodeId, fromTerminalId, toTerminalId, fiberCoordLength);
                   var connectCommandResult = await commandDispatcher.HandleAsync<ConnectTerminalsAtRouteNode, Result>(connectCommand);

                   if (connectCommandResult.IsFailed)
                       return new CommandResult(connectCommandResult);

                   return new CommandResult(Result.Ok());
               }
            );

           FieldAsync<CommandResultType>(
             "addAdditionalStructures",
             description: "Add additional terminal structure to terminal equipment - i.e. trays, cards etc.",
             arguments: new QueryArguments(
                 new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                 new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalEquipmentId" },
                 new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "structureSpecificationId" },
                 new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "numberOfStructures" },
                 new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "position" }
             ),
             resolve: async context =>
             {
                 var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                 var terminalEquipmentId = context.GetArgument<Guid>("terminalEquipmentId");
                 var structureSpecificationId = context.GetArgument<Guid>("structureSpecificationId");
                 var numberOfStructures = context.GetArgument<int>("numberOfStructures");
                 var position = context.GetArgument<int>("position");

                 var correlationId = Guid.NewGuid();

                 var userContext = context.UserContext as GraphQLUserContext;
                 var userName = userContext.Username;

                  // TODO: Get from work manager
                  var workTaskId = Guid.Parse("54800ae5-13a5-4b03-8626-a63b66a25568");

                 var commandUserContext = new UserContext(userName, workTaskId);

                 var addStructure = new PlaceAdditionalStructuresInTerminalEquipment(
                   correlationId: correlationId,
                   userContext: commandUserContext,
                   routeNodeId: routeNodeId,
                   terminalEquipmentId: terminalEquipmentId,
                   structureSpecificationId: structureSpecificationId,
                   numberOfStructures: numberOfStructures,
                   position: position
                 );

                 var addStructureResult = await commandDispatcher.HandleAsync<PlaceAdditionalStructuresInTerminalEquipment, Result>(addStructure);

                 return new CommandResult(addStructureResult);
             }
           );
        }
    }
}
