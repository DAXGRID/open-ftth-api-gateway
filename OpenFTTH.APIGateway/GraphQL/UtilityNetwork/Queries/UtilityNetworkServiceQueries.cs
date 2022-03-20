using FluentResults;
using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types;
using OpenFTTH.APIGateway.Util;
using OpenFTTH.CQRS;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;
using OpenFTTH.UtilityGraphService.API.Queries;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Queries
{
    public class UtilityNetworkServiceQueries : ObjectGraphType
    {
        public UtilityNetworkServiceQueries(ILogger<UtilityNetworkServiceQueries> logger, IQueryDispatcher queryDispatcher)
        {
            Description = "GraphQL API for querying data owned by utility network service";

            FieldAsync<ListGraphType<ManufacturerType>>(
                name: "manufacturers",
                description: "Retrieve all manufacturers.",
                resolve: async context =>
                {
                    var queryResult = await queryDispatcher.HandleAsync<GetManufacturer, Result<LookupCollection<Manufacturer>>>(new GetManufacturer());

                    return queryResult.Value;
                }
            );

            FieldAsync<ListGraphType<SpanEquipmentSpecificationType>>(
                name: "spanEquipmentSpecifications",
                description: "Retrieve all span equipment specifications.",
                resolve: async context =>
                {
                    var queryResult = await queryDispatcher.HandleAsync<GetSpanEquipmentSpecifications,
                        Result<LookupCollection<SpanEquipmentSpecification>>>(new GetSpanEquipmentSpecifications());

                    return queryResult.Value.OrderBy(s => s.Description);
                }
            );

            FieldAsync<ListGraphType<TerminalEquipmentSpecificationType>>(
                name: "terminalEquipmentSpecifications",
                description: "Retrieve all terminal equipment specifications.",
                resolve: async context =>
                {
                    var queryResult = await queryDispatcher.HandleAsync<GetTerminalEquipmentSpecifications,
                        Result<LookupCollection<TerminalEquipmentSpecification>>>(new GetTerminalEquipmentSpecifications());

                    return queryResult.Value.OrderBy(s => s.Description);
                }
            );

            FieldAsync<ListGraphType<NodeContainerSpecificationType>>(
                name: "nodeContainerSpecifications",
                description: "Retrieve all node container specifications.",
                resolve: async context =>
                {
                    var queryResult = await queryDispatcher.HandleAsync<GetNodeContainerSpecifications,
                        Result<LookupCollection<NodeContainerSpecification>>>(new GetNodeContainerSpecifications());

                    return queryResult.Value.OrderBy(s => s.Description);
                }
            );

            FieldAsync<ListGraphType<RackSpecificationType>>(
               name: "rackSpecifications",
               description: "Retrieve all rack specifications.",
               resolve: async context =>
               {
                   var queryResult = await queryDispatcher.HandleAsync<GetRackSpecifications, Result<LookupCollection<RackSpecification>>>(new GetRackSpecifications());

                   return queryResult.Value.OrderBy(s => s.Description);
               }
            );

            FieldAsync<SpanSegmentTraceType>(
                name: "spanSegmentTrace",
                description: "Trace a specific span segment",
                arguments: new QueryArguments(
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "spanSegmentId" }
                ),
                resolve: async context =>
                {
                    var spanSegmentId = context.GetArgument<Guid>("spanSegmentId");

                    // Get equipment information
                    var equipmentQueryResult = await queryDispatcher.HandleAsync<GetEquipmentDetails, FluentResults.Result<GetEquipmentDetailsResult>>(
                        new GetEquipmentDetails(new EquipmentIdList() { spanSegmentId })
                        {
                            EquipmentDetailsFilter = new EquipmentDetailsFilterOptions { IncludeRouteNetworkTrace = true }
                        }
                    );

                    if (equipmentQueryResult.IsFailed)
                    {
                        foreach (var error in equipmentQueryResult.Errors)
                            context.Errors.Add(new ExecutionError(error.Message));

                        return null;
                    }

                    if (equipmentQueryResult.Value.SpanEquipment == null || equipmentQueryResult.Value.SpanEquipment.Count == 0)
                    {
                        context.Errors.Add(new ExecutionError($"Cannot find any span equipment containing a span segment with id: {spanSegmentId}"));
                        return null;
                    }

                    if (equipmentQueryResult.Value.RouteNetworkTraces == null)
                    {
                        context.Errors.Add(new ExecutionError($"No trace information returned for span segment with id: {spanSegmentId}"));
                        return null;
                    }

                    if (equipmentQueryResult.Value.RouteNetworkTraces.Count != 1)
                    {
                        //context.Errors.Add(new ExecutionError($"Expected only one trace returned for span segment with id: {spanSegmentId}. Are you sure you did a query on a span segment id and not a span equipment id?"));
                        //return null;
                        logger.LogWarning($"Got {equipmentQueryResult.Value.RouteNetworkTraces.Count} trace results tracing span segment or equipment with id: {spanSegmentId}. Will just use the first trace returned.");
                    }

                    var theTrace = equipmentQueryResult.Value.RouteNetworkTraces.First();

                    return new SpanSegmentTrace()
                    {
                        RouteNetworkSegmentIds = theTrace.RouteSegmentIds,
                        RouteNetworkSegmentGeometries = UTM32WGS84Converter.ConvertGeoJsonLineStringsToWgs84(theTrace.RouteSegmentGeometries)
                    };
                });

            FieldAsync<TerminalEquipmentType>(
               name: "terminalEquipment",
               description: "Query information related to a specific terminal equipment",
               arguments: new QueryArguments(
                 new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalEquipmentOrTerminalId" }
               ),
               resolve: async context =>
               {
                   var terminalEquipmentOrTerminalId = context.GetArgument<Guid>("terminalEquipmentOrTerminalId");

                   // Get equipment information
                   var equipmentQueryResult = await queryDispatcher.HandleAsync<GetEquipmentDetails, FluentResults.Result<GetEquipmentDetailsResult>>(
                       new GetEquipmentDetails(new EquipmentIdList() { terminalEquipmentOrTerminalId })
                   );

                   if (equipmentQueryResult.IsFailed)
                   {
                       foreach (var error in equipmentQueryResult.Errors)
                           context.Errors.Add(new ExecutionError(error.Message));

                       return null;
                   }

                   if (equipmentQueryResult.Value.TerminalEquipment == null || equipmentQueryResult.Value.TerminalEquipment.Count == 0)
                   {
                       context.Errors.Add(new ExecutionError($"Cannot find any terminal equipment or terminal with id: {terminalEquipmentOrTerminalId}"));

                       return null;
                   }

                   var terminalEquipment = equipmentQueryResult.Value.TerminalEquipment.First();

                   return terminalEquipment;
               });

            FieldAsync<SpanEquipmentType>(
             name: "spanEquipment",
             description: "Query information related to a specific span equipment",
             arguments: new QueryArguments(
               new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "spanEquipmentOrSegmentId" }
             ),
             resolve: async context =>
             {
                 var spanEquipmentOrSegmentId = context.GetArgument<Guid>("spanEquipmentOrSegmentId");

                 // Get equipment information
                 var equipmentQueryResult = await queryDispatcher.HandleAsync<GetEquipmentDetails, FluentResults.Result<GetEquipmentDetailsResult>>(
                     new GetEquipmentDetails(new EquipmentIdList() { spanEquipmentOrSegmentId })
                 );

                 if (equipmentQueryResult.IsFailed)
                 {
                     foreach (var error in equipmentQueryResult.Errors)
                         context.Errors.Add(new ExecutionError(error.Message));

                     return null;
                 }

                 if (equipmentQueryResult.Value.SpanEquipment == null || equipmentQueryResult.Value.SpanEquipment.Count == 0)
                 {
                     context.Errors.Add(new ExecutionError($"Cannot find any span equipment containing a span segment with id: {spanEquipmentOrSegmentId}"));

                     return null;
                 }

                 var spanEquipment = equipmentQueryResult.Value.SpanEquipment.First();

                 return spanEquipment;
             });

            FieldAsync<NodeContainerType>(
                name: "nodeContainer",
                description: "Query information related to a specific node container",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "nodeContainerId" }
                ),
                resolve: async context =>
                {
                    var nodeContainerId = context.GetArgument<Guid>("nodeContainerId");

                    // Get equipment information
                    var equipmentQueryResult = await queryDispatcher.HandleAsync<GetEquipmentDetails, FluentResults.Result<GetEquipmentDetailsResult>>(
                        new GetEquipmentDetails(new InterestIdList() { nodeContainerId })
                    );

                    if (equipmentQueryResult.IsFailed)
                    {
                        foreach (var error in equipmentQueryResult.Errors)
                            context.Errors.Add(new ExecutionError(error.Message));

                        return null;
                    }

                    if (equipmentQueryResult.Value.NodeContainers == null || equipmentQueryResult.Value.NodeContainers.Count == 0)
                    {
                        context.Errors.Add(new ExecutionError($"Cannot find any node container with id: {nodeContainerId}"));

                        return null;
                    }

                    var nodeContainer = equipmentQueryResult.Value.NodeContainers.First();

                    return nodeContainer;
                }
            );

            Field<RackType>(
                name: "rack",
                description: "Query information related to a specific rack residing within a node",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "rackId" }
                ),
                resolve: context =>
                {
                    var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                    var rackId = context.GetArgument<Guid>("rackId");

                    var getNodeContainerResult = QueryHelper.GetNodeContainerFromRouteNodeId(queryDispatcher, routeNodeId);

                    if (getNodeContainerResult.IsFailed)
                    {
                        foreach (var error in getNodeContainerResult.Errors)
                            context.Errors.Add(new ExecutionError(error.Message));

                        return null;
                    }

                    var nodeContainer = getNodeContainerResult.Value;

                    if (nodeContainer.Racks == null || !nodeContainer.Racks.Any(r => r.Id == rackId))
                    {
                        context.Errors.Add(new ExecutionError($"Cannot find any rack with id: {rackId} within node container with id: {nodeContainer.Id}"));
                        return null;
                    }

                    return nodeContainer.Racks.First(r => r.Id == rackId);
                }
            );

            Field<ListGraphType<RackType>>(
                 name: "racks",
                 description: "Query all racks within node",
                 arguments: new QueryArguments(
                     new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" }
                 ),
                 resolve: context =>
                 {
                     var routeNodeId = context.GetArgument<Guid>("routeNodeId");

                     var getNodeContainerResult = QueryHelper.GetNodeContainerFromRouteNodeId(queryDispatcher, routeNodeId);

                     if (getNodeContainerResult.IsFailed)
                     {
                         foreach (var error in getNodeContainerResult.Errors)
                             context.Errors.Add(new ExecutionError(error.Message));

                         return null;
                     }

                     var nodeContainer = getNodeContainerResult.Value;

                     if (nodeContainer.Racks == null)
                     {
                         return new Rack[] { };
                     }

                     return nodeContainer.Racks;
                 }
             );

            FieldAsync<TerminalEquipmentAZConnectivityViewModelType>(
                name: "terminalEquipmentConnectivityView",
                description: "Query connectivity information related to ",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalEquipmentOrRackId" }
                ),
                resolve: async context =>
                {
                    var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                    var terminalEquipmentOrRackId = context.GetArgument<Guid>("terminalEquipmentOrRackId");

                    var connectivityQuery = new GetTerminalEquipmentConnectivityView(routeNodeId, terminalEquipmentOrRackId);

                    var connectivityQueryResult = await queryDispatcher.HandleAsync<GetTerminalEquipmentConnectivityView, Result<TerminalEquipmentAZConnectivityViewModel>>(
                        connectivityQuery
                    );

                    if (connectivityQueryResult.IsFailed)
                    {
                        foreach (var error in connectivityQueryResult.Errors)
                            context.Errors.Add(new ExecutionError(error.Message));

                        return null;
                    }

                    return connectivityQueryResult.Value;
                }
            );

            FieldAsync<ConnectivityTraceViewType>(
                name: "connectivityTraceView",
                description: "Trace connectivity",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNetworkElementId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalOrSpanEquipmentId" }
                ),
                resolve: async context =>
                {
                    var routeNetworkElementId = context.GetArgument<Guid>("routeNetworkElementId");
                    var terminalOrSpanEquipmentId = context.GetArgument<Guid>("terminalOrSpanEquipmentId");

                    var connectivityQuery = new GetConnectivityTraceView(routeNetworkElementId, terminalOrSpanEquipmentId);

                    var connectivityQueryResult = await queryDispatcher.HandleAsync<GetConnectivityTraceView, Result<ConnectivityTraceView>>(
                        connectivityQuery
                    );

                    if (connectivityQueryResult.IsFailed)
                    {
                        foreach (var error in connectivityQueryResult.Errors)
                            context.Errors.Add(new ExecutionError(error.Message));
                        return null;
                    }

                    return connectivityQueryResult.Value;
                }
            );

            FieldAsync<SpanEquipmentAZConnectivityViewModelType>(
                name: "spanEquipmentConnectivityView",
                description: "Query connectivity information related to span equipment",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNetworkElementId" },
                    new QueryArgument<NonNullGraphType<ListGraphType<IdGraphType>>> { Name = "spanEquipmentOrSegmentIds" }
                ),
                resolve: async context =>
                {
                    var routeNetworkElementId = context.GetArgument<Guid>("routeNetworkElementId");
                    var spanEquipmentOrSegmentIds = context.GetArgument<Guid[]>("spanEquipmentOrSegmentIds");

                    var connectivityQuery = new GetSpanEquipmentConnectivityView(routeNetworkElementId, spanEquipmentOrSegmentIds);

                    var connectivityQueryResult = await queryDispatcher.HandleAsync<GetSpanEquipmentConnectivityView, Result<SpanEquipmentAZConnectivityViewModel>>(
                      connectivityQuery
                    );

                    if (connectivityQueryResult.IsFailed)
                    {
                        foreach (var error in connectivityQueryResult.Errors)
                            context.Errors.Add(new ExecutionError(error.Message));

                        return null;
                    }

                    return connectivityQueryResult.Value;
                }
            );

            FieldAsync<SpanEquipmentPassageViewModelType>(
                name: "spanEquipmentPassageView",
                description: "Query passage information related to span equipment",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNetworkElementId" },
                    new QueryArgument<NonNullGraphType<ListGraphType<IdGraphType>>> { Name = "spanEquipmentOrSegmentIds" }
                ),
                resolve: async context =>
                {
                    var routeNetworkElementId = context.GetArgument<Guid>("routeNetworkElementId");
                    var spanEquipmentOrSegmentIds = context.GetArgument<Guid[]>("spanEquipmentOrSegmentIds");

                    var connectivityQuery = new GetSpanEquipmentPassageView(routeNetworkElementId, spanEquipmentOrSegmentIds);

                    var connectivityQueryResult = await queryDispatcher.HandleAsync<GetSpanEquipmentPassageView, Result<SpanEquipmentPassageViewModel>>(
                      connectivityQuery
                    );

                    if (connectivityQueryResult.IsFailed)
                    {
                        foreach (var error in connectivityQueryResult.Errors)
                            context.Errors.Add(new ExecutionError(error.Message));

                        return null;
                    }

                    return connectivityQueryResult.Value;
                }
            );

            FieldAsync<ListGraphType<ConnectivityFaceType>>(
                name: "connectivityFaces",
                description: "Query terminal equipment sides and fiber cable ends available for connectivity (aka connectivity faces)",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" }
                ),
                resolve: async context =>
                {
                    var routeNodeId = context.GetArgument<Guid>("routeNodeId");

                    var connectivityFacesQuery = new GetConnectivityFaces(routeNodeId);

                    var connectivityFacesQueryResult = await queryDispatcher.HandleAsync<GetConnectivityFaces, Result<List<ConnectivityFace>>>(
                      connectivityFacesQuery
                    );

                    if (connectivityFacesQueryResult.IsFailed)
                    {
                        foreach (var error in connectivityFacesQueryResult.Errors)
                            context.Errors.Add(new ExecutionError(error.Message));

                        return null;
                    }

                    return connectivityFacesQueryResult.Value;
                }
            );

            FieldAsync<ListGraphType<ConnectivityFaceConnectionType>>(
                name: "connectivityFaceConnections",
                description: "Query the connections available in a connectivity face",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                    new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "spanOrTerminalEquipmentId" },
                    new QueryArgument<NonNullGraphType<FaceKindEnumType>> { Name = "faceType" }
                ),
                resolve: async context =>
                {
                    var routeNodeId = context.GetArgument<Guid>("routeNodeId");
                    var spanOrTerminalEquipmentId = context.GetArgument<Guid>("spanOrTerminalEquipmentId");
                    var faceType = context.GetArgument<FaceKindEnum>("faceType");

                    var connectivityFacesConnectionsQuery = new GetConnectivityFaceConnections(routeNodeId, spanOrTerminalEquipmentId, faceType);

                    var connectivityFaceConnectionsQueryResult = await queryDispatcher.HandleAsync<GetConnectivityFaceConnections, Result<List<ConnectivityFaceConnection>>>(
                      connectivityFacesConnectionsQuery
                    );

                    if (connectivityFaceConnectionsQueryResult.IsFailed)
                    {
                        foreach (var error in connectivityFaceConnectionsQueryResult.Errors)
                            context.Errors.Add(new ExecutionError(error.Message));

                        return null;
                    }

                    return connectivityFaceConnectionsQueryResult.Value;
                }
            );


            FieldAsync<DisconnectSpanEquipmentFromTerminalViewType>(
               name: "disconnectSpanEquipmentFromTerminalView",
               description: "Information needed to show disconnect information to user",
               arguments: new QueryArguments(
                   new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "spanSegmentId" },
                   new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalId" }
               ),
               resolve: async context =>
               {
                   var spanSegmentId = context.GetArgument<Guid>("spanSegmentId");
                   var terminalId = context.GetArgument<Guid>("terminalId");

                   var query = new GetDisconnectSpanEquipmentFromTerminalView(spanSegmentId, terminalId);

                   var queryResult = await queryDispatcher.HandleAsync<GetDisconnectSpanEquipmentFromTerminalView, Result<DisconnectSpanEquipmentFromTerminalView>>(
                       query
                   );

                   if (queryResult.IsFailed)
                   {
                       foreach (var error in queryResult.Errors)
                           context.Errors.Add(new ExecutionError(error.Message));
                       return null;
                   }

                   return queryResult.Value;
               }
           );
        }
    }
}
