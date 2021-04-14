using CSharpFunctionalExtensions;
using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types;
using OpenFTTH.CQRS;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
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

            Field<ListGraphType<ManufacturerType>>(
                name: "manufacturers",
                description: "Retrieve all manufacturers.",
                resolve: context =>
                {
                    var queryResult = queryDispatcher.HandleAsync<GetManufacturer, Result<LookupCollection<Manufacturer>>>(new GetManufacturer()).Result;

                    return queryResult.Value;
                }
            );

            Field<ListGraphType<SpanEquipmentSpecificationType>>(
                name: "spanEquipmentSpecifications",
                description: "Retrieve all span equipment specifications.",
                resolve: context =>
                {
                    var userContext = context.UserContext as GraphQLUserContext;
                    var username = userContext.User?.Claims.FirstOrDefault(x => x.Type == "preferred_username").Value;

                    logger.LogError($"username: {username}");

                    var queryResult = queryDispatcher.HandleAsync<GetSpanEquipmentSpecifications, Result<LookupCollection<SpanEquipmentSpecification>>>(new GetSpanEquipmentSpecifications()).Result;
                    return queryResult.Value.OrderBy(s => s.Description);
                }
            );

            Field<ListGraphType<NodeContainerSpecificationType>>(
                name: "nodeContainerSpecifications",
                description: "Retrieve all node container specifications.",
                resolve: context =>
                {
                    var queryResult = queryDispatcher.HandleAsync<GetNodeContainerSpecifications, Result<LookupCollection<NodeContainerSpecification>>>(new GetNodeContainerSpecifications()).Result;

                    return queryResult.Value.OrderBy(s => s.Description);
                }
            );

            Field<SpanSegmentTraceType>(
                name: "spanSegmentTrace",
                description: "Trace a specific span segment",
                arguments: new QueryArguments(
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "spanSegmentId" }
                ),
                resolve: context =>
                {
                    var spanSegmentId = context.GetArgument<Guid>("spanSegmentId");

                    // Get equipment information
                    var equipmentQueryResult = queryDispatcher.HandleAsync<GetEquipmentDetails, FluentResults.Result<GetEquipmentDetailsResult>>(
                        new GetEquipmentDetails(new EquipmentIdList() { spanSegmentId })
                    ).Result;

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


                    var spanEquipment = equipmentQueryResult.Value.SpanEquipment.First();

                    // Get walk of interest of the span equipment
                    var interestQueryResult = queryDispatcher.HandleAsync<GetRouteNetworkDetails, FluentResults.Result<GetRouteNetworkDetailsResult>>(
                        new GetRouteNetworkDetails(new InterestIdList() { spanEquipment.WalkOfInterestId })
                    ).Result;

                    if (interestQueryResult.IsFailed)
                    {
                        foreach (var error in interestQueryResult.Errors)
                            context.Errors.Add(new ExecutionError(error.Message));

                        return null;
                    }

                    var interest = interestQueryResult.Value.Interests.First();

                    // Create trace object
                    List<Guid> segmentIds = new List<Guid>();

                    for (int i = 1; i < interest.RouteNetworkElementRefs.Count; i += 2)
                    {
                        segmentIds.Add(interest.RouteNetworkElementRefs[i]);
                    }

                    return new SpanSegmentTrace()
                    {
                        RouteNetworkSegmentIds = segmentIds.ToArray()
                    };
                }
            );


            Field<SpanEquipmentType>(
               name: "spanEquipment",
               description: "Query information related to a specific span equipment",
               arguments: new QueryArguments(
                 new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "spanEquipmentOrSegmentId" }
               ),
               resolve: context =>
               {
                   var spanEquipmentOrSegmentId = context.GetArgument<Guid>("spanEquipmentOrSegmentId");

                   // Get equipment information
                   var equipmentQueryResult = queryDispatcher.HandleAsync<GetEquipmentDetails, FluentResults.Result<GetEquipmentDetailsResult>>(
                       new GetEquipmentDetails(new EquipmentIdList() { spanEquipmentOrSegmentId })
                   ).Result;

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
               }
           );
        }
    }
}
