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
        public UtilityNetworkServiceQueries(ILogger<UtilityNetworkServiceQueries> logger, IQueryDispatcher queryDispatcher, UTM32WGS84Converter coordinateConverter)
        {
            Description = "GraphQL API for querying data owned by utility network service";

            Field<ListGraphType<ManufacturerType>>("manufacturers")
                .Description("Retrieve all manufacturers.")
                .ResolveAsync(async context =>
                {
                    var queryResult = await queryDispatcher.HandleAsync<GetManufacturer, Result<LookupCollection<Manufacturer>>>(new GetManufacturer());
                    return queryResult.Value;
                });

            Field<ListGraphType<SpanEquipmentSpecificationType>>("spanEquipmentSpecifications")
                .Description("Retrieve all span equipment specifications.")
                .ResolveAsync(async context =>
                {
                    var queryResult = await queryDispatcher.HandleAsync<GetSpanEquipmentSpecifications,
                        Result<LookupCollection<SpanEquipmentSpecification>>>(new GetSpanEquipmentSpecifications());
                    return queryResult.Value.OrderBy(s => s.Description);
                });

            Field<ListGraphType<TerminalEquipmentSpecificationType>>("terminalEquipmentSpecifications")
                .Description("Retrieve all terminal equipment specifications.")
                .ResolveAsync(async context =>
                {
                    var queryResult = await queryDispatcher.HandleAsync<GetTerminalEquipmentSpecifications,
                        Result<LookupCollection<TerminalEquipmentSpecification>>>(new GetTerminalEquipmentSpecifications());

                    return queryResult.Value.OrderBy(s => s.Description);
                });

            Field<ListGraphType<TerminalStructureSpecificationType>>("terminalStructureSpecifications")
                .Description("Retrieve all terminal structure specifications - i.e. cards, tray etc.")
                .ResolveAsync(async context =>
                {
                    var queryResult = await queryDispatcher.HandleAsync<GetTerminalStructureSpecifications,
                        Result<LookupCollection<TerminalStructureSpecification>>>(new GetTerminalStructureSpecifications());

                    return queryResult.Value.OrderBy(s => s.Description);
                });

            Field<ListGraphType<NodeContainerSpecificationType>>("nodeContainerSpecifications")
                .Description("Retrieve all node container specifications.")
                .ResolveAsync(async context =>
                {
                    var queryResult = await queryDispatcher.HandleAsync<GetNodeContainerSpecifications,
                        Result<LookupCollection<NodeContainerSpecification>>>(new GetNodeContainerSpecifications());

                    return queryResult.Value.OrderBy(s => s.Description);
                });

            Field<ListGraphType<RackSpecificationType>>("rackSpecifications")
                .Description("Retrieve all rack specifications.")
                .ResolveAsync(async context =>
                {
                    var queryResult = await queryDispatcher.HandleAsync<GetRackSpecifications, Result<LookupCollection<RackSpecification>>>(new GetRackSpecifications());
                    return queryResult.Value.OrderBy(s => s.Description);
                });

            Field<SpanSegmentTraceType>("spanSegmentTrace")
                .Description("Trace one or more span segments")
                .Arguments(new QueryArguments(new QueryArgument<NonNullGraphType<ListGraphType<IdGraphType>>> { Name = "spanSegmentIds" }))
                .ResolveAsync(async context =>
                {
                    var spanSegmentIds = context.GetArgument<List<Guid>>("spanSegmentIds");

                    if (spanSegmentIds.Count == 0)
                    {
                        context.Errors.Add(new ExecutionError($"spanSegmentIds parameter cannot be empty"));
                        return null;
                    }

                    List<string> segmentJsonGeometries = new();
                    List<Guid> routeSegmentIds = new();

                    foreach (var spanSegmentId in spanSegmentIds)
                    {
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

                        segmentJsonGeometries.AddRange(theTrace.RouteSegmentGeometries);
                        routeSegmentIds.AddRange(theTrace.RouteSegmentIds);
                    }

                    var coordinateConverterResult = coordinateConverter.ConvertGeoJsonLineStringsToWgs84(segmentJsonGeometries.ToArray());

                    return new SpanSegmentTrace()
                    {
                        RouteNetworkSegmentIds = routeSegmentIds.ToArray(),
                        RouteNetworkSegmentGeometries = coordinateConverterResult.WGS84GeoJsonStrings,
                        WGS84MinX = coordinateConverterResult.WGS84BoundingBox.MinX,
                        WGS84MinY = coordinateConverterResult.WGS84BoundingBox.MinY,
                        WGS84MaxX = coordinateConverterResult.WGS84BoundingBox.MaxX,
                        WGS84MaxY = coordinateConverterResult.WGS84BoundingBox.MaxY,
                        ETRS89MinX = coordinateConverterResult.ETRS89BoundingBox.MinX,
                        ETRS89MinY = coordinateConverterResult.ETRS89BoundingBox.MinY,
                        ETRS89MaxX = coordinateConverterResult.ETRS89BoundingBox.MaxX,
                        ETRS89MaxY = coordinateConverterResult.ETRS89BoundingBox.MaxY,
                    };
                });

            Field<TerminalEquipmentType>("terminalEquipment")
                .Description("Query information related to a specific terminal equipment")
                .Arguments(new QueryArguments(new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalEquipmentOrTerminalId" }))
                .ResolveAsync(async context =>
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

            Field<TerminalStructureType>("terminalStructure")
                .Description("Query information related to a specific terminal structure")
                .Arguments(new QueryArguments(
                               new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalEquipmentOrTerminalId" },
                               new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalStructureId" })
                )
                .ResolveAsync(async context =>
                {
                    var terminalEquipmentOrTerminalId = context.GetArgument<Guid>("terminalEquipmentOrTerminalId");
                    var terminalStructureId = context.GetArgument<Guid>("terminalStructureId");

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

                    if (!terminalEquipment.TerminalStructures.Any(t => t.Id == terminalStructureId))
                    {
                        context.Errors.Add(new ExecutionError($"Cannot find any terminal structure with id: {terminalStructureId} in terminal equipment with id: {terminalEquipmentOrTerminalId}"));

                        return null;
                    }

                    return terminalEquipment.TerminalStructures.First(t => t.Id == terminalStructureId);
                });

            Field<SpanEquipmentType>("spanEquipment")
                .Description("Query information related to a specific span equipment")
                .Arguments(new QueryArguments(new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "spanEquipmentOrSegmentId" }))
                .ResolveAsync(async context =>
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

            Field<NodeContainerType>("nodeContainer")
                .Description("Query information related to a specific node container")
                .Arguments(new QueryArguments(new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "nodeContainerId" }))
                .ResolveAsync(async context =>
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
                });

            Field<RackType>("rack")
                .Description("Query information related to a specific rack residing within a node")
                .Arguments(
                    new QueryArguments(
                        new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                        new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "rackId" }))
                .Resolve(context =>
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
                });

            Field<ListGraphType<RackType>>("racks")
                .Description("Query all racks within node")
                .Arguments(new QueryArguments(
                               new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" }))
                .Resolve(context =>
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
                });

            Field<TerminalEquipmentAZConnectivityViewModelType>("terminalEquipmentConnectivityView")
                .Description("Query connectivity information related to")
                .Arguments(new QueryArguments(
                               new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                               new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalEquipmentOrRackId" }))
                .ResolveAsync(async context =>
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
                });

            Field<ConnectivityTraceViewType>("connectivityTraceView")
                .Description("Trace connectivity")
                .Arguments(new QueryArguments(
                               new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNetworkElementId" },
                               new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalOrSpanEquipmentId" }))
                .ResolveAsync(async context =>
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
                });

            Field<SpanEquipmentAZConnectivityViewModelType>("spanEquipmentConnectivityView")
                .Description("Query connectivity information related to span equipment")
                .Arguments(new QueryArguments(
                               new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNetworkElementId" },
                               new QueryArgument<NonNullGraphType<ListGraphType<IdGraphType>>> { Name = "spanEquipmentOrSegmentIds" }))
                .ResolveAsync(async context =>
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
                });

            Field<SpanEquipmentPassageViewModelType>("spanEquipmentPassageView")
                .Description("Query passage information related to span equipment")
                .Arguments(new QueryArguments(
                               new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNetworkElementId" },
                               new QueryArgument<NonNullGraphType<ListGraphType<IdGraphType>>> { Name = "spanEquipmentOrSegmentIds" }))
                .ResolveAsync(async context =>
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
                });

            Field<ListGraphType<ConnectivityFaceType>>("connectivityFaces")
                .Description("Query terminal equipment sides and fiber cable ends available for connectivity (aka connectivity faces)")
                .Arguments(new QueryArguments(
                               new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" }))
                .ResolveAsync(async context =>
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
                });

            Field<ListGraphType<ConnectivityFaceConnectionType>>("connectivityFaceConnections")
                .Description("Query the connections available in a connectivity face")
                .Arguments(new QueryArguments(
                               new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "routeNodeId" },
                               new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "spanOrTerminalEquipmentId" },
                               new QueryArgument<NonNullGraphType<FaceKindEnumType>> { Name = "faceType" }))
                .ResolveAsync(async context =>
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
                });


            Field<DisconnectSpanEquipmentFromTerminalViewType>("disconnectSpanEquipmentFromTerminalView")
                .Description("Information needed to show disconnect information to user")
                .Arguments(new QueryArguments(
                               new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "spanSegmentId" },
                               new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "terminalId" }))
                .ResolveAsync(async context =>
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
                });
        }
    }
}
