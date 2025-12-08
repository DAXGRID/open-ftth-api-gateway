using OpenFTTH.Results;
using GraphQL;
using GraphQL.Types;
using OpenFTTH.APIGateway.CoreTypes;
using OpenFTTH.APIGateway.GraphQL.Addresses.Types;
using OpenFTTH.APIGateway.GraphQL.Core.Model;
using OpenFTTH.APIGateway.GraphQL.RouteNetwork.Types;
using OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types;
using OpenFTTH.APIGateway.GraphQL.Work;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.APIGateway.GraphQL.RouteNetwork.Mutations
{
    public class TerminalEquipmentMutations : ObjectGraphType
    {
        public TerminalEquipmentMutations(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IEventStore eventStore)
        {
            Description = "Terminal equipment mutations";

            Field<CommandResultType>("updateProperties")
                .Description("Mutation that can be used to change the terminal equipment specification,naming information")
                .Arguments(new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalEquipmentId" },
                    new QueryArgument<IdGraphType> { Name = "terminalEquipmentSpecificationId" },
                    new QueryArgument<IdGraphType> { Name = "manufacturerId" },
                    new QueryArgument<NamingInfoInputType> { Name = "namingInfo" },
                    new QueryArgument<AddressInfoInputType> { Name = "addressInfo" },
                    new QueryArgument<IdGraphType> { Name = "rackId" },
                    new QueryArgument<IntGraphType> { Name = "rackStartUnitPosition" }
                ))
                .ResolveAsync(async context =>
                {
                    var terminalEquipmentId = context.GetArgument<Guid>("terminalEquipmentId");

                    var correlationId = Guid.NewGuid();

                    var userContext = context.UserContext as GraphQLUserContext;
                    var userName = userContext.Username;

                    // Get the users current work task (will fail, if user has not selected a work task)
                    var currentWorkTaskIdResult = WorkQueryHelper.GetUserCurrentWorkId(userName, queryDispatcher);

                    if (currentWorkTaskIdResult.IsFailed)
                        return new CommandResult(currentWorkTaskIdResult);

                    var commandUserContext = new UserContext(userName, currentWorkTaskIdResult.Value);


                    var updateCmd = new UpdateTerminalEquipmentProperties(correlationId, commandUserContext, terminalEquipmentId: terminalEquipmentId)
                    {
                        SpecificationId = context.HasArgument("terminalEquipmentSpecificationId") ? context.GetArgument<Guid>("terminalEquipmentSpecificationId") : null,
                        ManufacturerId = context.HasArgument("manufacturerId") ? context.GetArgument<Guid>("manufacturerId") : null,
                        NamingInfo = context.HasArgument("namingInfo") ? context.GetArgument<NamingInfo>("namingInfo") : null,
                        AddressInfo = context.HasArgument("addressInfo") ? context.GetArgument<AddressInfo>("addressInfo") : null,
                        RackId = context.HasArgument("rackId") ? context.GetArgument<Guid>("rackId") : null,
                        StartUnitPosition = context.HasArgument("rackStartUnitPosition") ? context.GetArgument<int>("rackStartUnitPosition") : null,
                    };

                    var updateResult = await commandDispatcher.HandleAsync<UpdateTerminalEquipmentProperties, Result>(updateCmd);

                    return new CommandResult(updateResult);
                });

            Field<CommandResultType>("updateTerminalStructureProperties")
                .Description("Mutation that can be used to change a terminal structure (card, tray etc) that sits a terminal equipment")
                .Arguments(new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalEquipmentId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalStructureId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalStructureSpecificationId" },
                    new QueryArgument<IntGraphType> { Name = "position" },
                    new QueryArgument<InterfaceInfoInputType> { Name = "interfaceInfo" }
                ))
                .ResolveAsync(async context =>
                {
                    var terminalEquipmentId = context.GetArgument<Guid>("terminalEquipmentId");
                    var terminalStructureId = context.GetArgument<Guid>("terminalStructureId");

                    var correlationId = Guid.NewGuid();

                    var userContext = context.UserContext as GraphQLUserContext;
                    var userName = userContext.Username;

                    // Get the users current work task (will fail, if user has not selected a work task)
                    var currentWorkTaskIdResult = WorkQueryHelper.GetUserCurrentWorkId(userName, queryDispatcher);

                    if (currentWorkTaskIdResult.IsFailed)
                        return new CommandResult(currentWorkTaskIdResult);

                    var commandUserContext = new UserContext(userName, currentWorkTaskIdResult.Value);


                    var updateCmd = new UpdateTerminalStructureProperties(correlationId, commandUserContext, terminalEquipmentId: terminalEquipmentId, terminalStructureId: terminalStructureId)
                    {
                        StructureSpecificationId = context.GetArgument<Guid>("terminalStructureSpecificationId"),
                        Position = context.GetArgument<int>("position"),
                        InterfaceInfo = context.GetArgument<InterfaceInfo>("interfaceInfo")
                    };

                    var updateResult = await commandDispatcher.HandleAsync<UpdateTerminalStructureProperties, Result>(updateCmd);

                    return new CommandResult(updateResult);
                });

            Field<CommandResultType>("remove")
                .Description("Remove the terminal equipment")
                .Arguments(new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalEquipmentId" }
                ))
                .ResolveAsync(async context =>
                {
                    var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                    var terminalEquipmentId = context.GetArgument<Guid>("terminalEquipmentId");

                    var correlationId = Guid.NewGuid();

                    var userContext = context.UserContext as GraphQLUserContext;
                    var userName = userContext.Username;

                    // Get the users current work task (will fail, if user has not selected a work task)
                    var currentWorkTaskIdResult = WorkQueryHelper.GetUserCurrentWorkId(userName, queryDispatcher);

                    if (currentWorkTaskIdResult.IsFailed)
                        return new CommandResult(currentWorkTaskIdResult);

                    var commandUserContext = new UserContext(userName, currentWorkTaskIdResult.Value);

                    var removeTerminalEquipment = new RemoveTerminalEquipment(
                        correlationId: correlationId,
                        userContext: commandUserContext,
                        terminalEquipmentId: terminalEquipmentId
                    );

                    var removeResult = await commandDispatcher.HandleAsync<RemoveTerminalEquipment, Result>(removeTerminalEquipment);

                    return new CommandResult(removeResult);
                });

            Field<CommandResultType>("connectTerminals")
                .Description("Connect terminals - i.e. create a patch from one terminal equipment to another")
                .Arguments(new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "fromTerminalId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "toTerminalId" },
                    new QueryArgument<NonNullGraphType<FloatGraphType>> { Name = "fiberCoordLength" }
                ))
                .ResolveAsync(async context =>
                {
                    var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                    var fromTerminalId = context.GetArgument<Guid>("fromTerminalId");
                    var toTerminalId = context.GetArgument<Guid>("toTerminalId");
                    var fiberCoordLength = context.GetArgument<Double>("fiberCoordLength");

                    var correlationId = Guid.NewGuid();

                    var userContext = context.UserContext as GraphQLUserContext;
                    var userName = userContext.Username;

                    // Get the users current work task (will fail, if user has not selected a work task)
                    var currentWorkTaskIdResult = WorkQueryHelper.GetUserCurrentWorkId(userName, queryDispatcher);

                    if (currentWorkTaskIdResult.IsFailed)
                        return new CommandResult(currentWorkTaskIdResult);

                    var commandUserContext = new UserContext(userName, currentWorkTaskIdResult.Value);

                    var connectCommand = new ConnectTerminalsAtRouteNode(correlationId, commandUserContext, routeNodeId, fromTerminalId, toTerminalId, fiberCoordLength);
                    var connectCommandResult = await commandDispatcher.HandleAsync<ConnectTerminalsAtRouteNode, Result>(connectCommand);

                    if (connectCommandResult.IsFailed)
                        return new CommandResult(connectCommandResult);

                    return new CommandResult(Result.Ok());
                });

            Field<CommandResultType>("addAdditionalStructures")
                .Description("Add additional terminal structure to terminal equipment - i.e. trays, cards etc.")
                .Arguments(new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalEquipmentId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "structureSpecificationId" },
                    new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "numberOfStructures" },
                    new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "position" }
                ))
                .ResolveAsync(async context =>
                {
                    var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                    var terminalEquipmentId = context.GetArgument<Guid>("terminalEquipmentId");
                    var structureSpecificationId = context.GetArgument<Guid>("structureSpecificationId");
                    var numberOfStructures = context.GetArgument<int>("numberOfStructures");
                    var position = context.GetArgument<int>("position");

                    var correlationId = Guid.NewGuid();

                    var userContext = context.UserContext as GraphQLUserContext;
                    var userName = userContext.Username;

                    // Get the users current work task (will fail, if user has not selected a work task)
                    var currentWorkTaskIdResult = WorkQueryHelper.GetUserCurrentWorkId(userName, queryDispatcher);

                    if (currentWorkTaskIdResult.IsFailed)
                        return new CommandResult(currentWorkTaskIdResult);

                    var commandUserContext = new UserContext(userName, currentWorkTaskIdResult.Value);

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
                });

            Field<CommandResultType>("addAdditionalStructure")
               .Description("Add additional terminal structure to terminal equipment - i.e. trays, cards etc.")
               .Arguments(new QueryArguments(
                   new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                   new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalEquipmentId" },
                   new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "structureSpecificationId" },
                   new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "name" },
                   new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "position" }
               ))
               .ResolveAsync(async context =>
               {
                   var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                   var terminalEquipmentId = context.GetArgument<Guid>("terminalEquipmentId");
                   var structureSpecificationId = context.GetArgument<Guid>("structureSpecificationId");
                   var name = context.GetArgument<string>("name");
                   var position = context.GetArgument<int>("position");

                   var correlationId = Guid.NewGuid();

                   var userContext = context.UserContext as GraphQLUserContext;
                   var userName = userContext.Username;

                   // Get the users current work task (will fail, if user has not selected a work task)
                   var currentWorkTaskIdResult = WorkQueryHelper.GetUserCurrentWorkId(userName, queryDispatcher);

                   if (currentWorkTaskIdResult.IsFailed)
                       return new CommandResult(currentWorkTaskIdResult);

                   var commandUserContext = new UserContext(userName, currentWorkTaskIdResult.Value);

                   var addStructure = new PlaceAdditionalStructureInTerminalEquipment(
                       correlationId: correlationId,
                       userContext: commandUserContext,
                       routeNodeId: routeNodeId,
                       terminalEquipmentId: terminalEquipmentId,
                       structureSpecificationId: structureSpecificationId,
                       name: name,
                       position: position
                   );

                   var addStructureResult = await commandDispatcher.HandleAsync<PlaceAdditionalStructureInTerminalEquipment, Result>(addStructure);

                   return new CommandResult(addStructureResult);
               });

            Field<CommandResultType>("addInterface")
                .Description("Add interface to optical line terminal equipment")
                .Arguments(new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalEquipmentId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "structureSpecificationId" },
                    new QueryArgument<InterfaceInfoInputType> { Name = "interfaceInfo" }
                ))
                .ResolveAsync(async context =>
                {
                    var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                    var terminalEquipmentId = context.GetArgument<Guid>("terminalEquipmentId");
                    var structureSpecificationId = context.GetArgument<Guid>("structureSpecificationId");

                    var correlationId = Guid.NewGuid();

                    var userContext = context.UserContext as GraphQLUserContext;
                    var userName = userContext.Username;

                    // Get the users current work task (will fail, if user has not selected a work task)
                    var currentWorkTaskIdResult = WorkQueryHelper.GetUserCurrentWorkId(userName, queryDispatcher);

                    if (currentWorkTaskIdResult.IsFailed)
                        return new CommandResult(currentWorkTaskIdResult);

                    var commandUserContext = new UserContext(userName, currentWorkTaskIdResult.Value);

                    var addStructure = new PlaceAdditionalStructuresInTerminalEquipment(
                        correlationId: correlationId,
                        userContext: commandUserContext,
                        routeNodeId: routeNodeId,
                        terminalEquipmentId: terminalEquipmentId,
                        structureSpecificationId: structureSpecificationId,
                        numberOfStructures: 1,
                        position: 0
                    )
                    {
                        InterfaceInfo = context.HasArgument("interfaceInfo") ? context.GetArgument<InterfaceInfo>("interfaceInfo") : null
                    };

                    var addStructureResult = await commandDispatcher.HandleAsync<PlaceAdditionalStructuresInTerminalEquipment, Result>(addStructure);

                    return new CommandResult(addStructureResult);
                });

            Field<CommandResultType>("removeStructure")
                .Description("Remove terminal structure from equipment")
                .Arguments(new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalEquipmentId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalStructureId" }
                ))
                .ResolveAsync(async context =>
                {
                    var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                    var terminalEquipmentId = context.GetArgument<Guid>("terminalEquipmentId");
                    var terminalStructureId = context.GetArgument<Guid>("terminalStructureId");

                    var correlationId = Guid.NewGuid();

                    var userContext = context.UserContext as GraphQLUserContext;
                    var userName = userContext.Username;

                    // Get the users current work task (will fail, if user has not selected a work task)
                    var currentWorkTaskIdResult = WorkQueryHelper.GetUserCurrentWorkId(userName, queryDispatcher);

                    if (currentWorkTaskIdResult.IsFailed)
                        return new CommandResult(currentWorkTaskIdResult);

                    var commandUserContext = new UserContext(userName, currentWorkTaskIdResult.Value);

                    var removeStructure = new RemoveTerminalStructureFromTerminalEquipment(
                        correlationId: correlationId,
                        userContext: commandUserContext,
                        routeNodeId: routeNodeId,
                        terminalEquipmentId: terminalEquipmentId,
                        terminalStructureId: terminalStructureId
                    );

                    var removeStructureResult = await commandDispatcher.HandleAsync<RemoveTerminalStructureFromTerminalEquipment, Result>(removeStructure);

                    return new CommandResult(removeStructureResult);
                });


            Field<CommandResultType>("updateTags")
           .Description("Mutation that update tags on a specific terminal og span equipment")
           .Arguments(new QueryArguments(
               new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalOrSpanEquipmentId" },
               new QueryArgument<NonNullGraphType<ListGraphType<EquipmentTagType>>> { Name = "tags" }
           ))
           .ResolveAsync(async context =>
           {
               var terminalOrSpanEquipmentId = context.GetArgument<Guid>("terminalOrSpanEquipmentId");
               var tags = context.GetArgument<EquipmentTag[]>("tags");

               var correlationId = Guid.NewGuid();

               var userContext = context.UserContext as GraphQLUserContext;
               var userName = userContext.Username;

               // Get the users current work task (will fail, if user has not selected a work task)
               var currentWorkTaskIdResult = WorkQueryHelper.GetUserCurrentWorkId(userName, queryDispatcher);

               if (currentWorkTaskIdResult.IsFailed)
                   return new CommandResult(currentWorkTaskIdResult);

               var commandUserContext = new UserContext(userName, currentWorkTaskIdResult.Value);

               var updateCmd = new UpdateTags(correlationId, commandUserContext, terminalOrSpanEquipmentId: terminalOrSpanEquipmentId, tags: tags);

               var updateResult = await commandDispatcher.HandleAsync<UpdateTags, Result>(updateCmd);

               return new CommandResult(updateResult);
           });
        }
    }
}
