using OpenFTTH.Results;
using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.CQRS;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types
{
    public class SpanEquipmentType : ObjectGraphType<SpanEquipment>
    {
        public SpanEquipmentType(ILogger<SpanEquipmentType> logger, IQueryDispatcher queryDispatcher)
        {
            Field(x => x.Id, type: typeof(IdGraphType)).Description("Master Resource Identifier UUID Property");
            Field(x => x.Name, type: typeof(StringGraphType)).Description("Short name");
            Field(x => x.Description, type: typeof(StringGraphType)).Description("Long description");
            Field(x => x.MarkingInfo, type: typeof(MarkingInfoType)).Description("Text and color marking information");
            Field(x => x.AddressInfo, type: typeof(AddressInfoType)).Description("Address information such as access and unit address id");
            Field(x => x.SpecificationId, type: typeof(IdGraphType)).Description("Span equipment specification id");
            Field(x => x.ManufacturerId, type: typeof(IdGraphType)).Description("Span equipment manufacturer id");
            Field(x => x.IsCable, type: typeof(BooleanGraphType)).Description("True if span equipment is a cable. Otherwise it's a conduit.");

            Field<SpanEquipmentSpecificationType>("specification")
               .Description("The specification used to create the span equipment")
               .ResolveAsync(async context =>
               {
                   var queryResult = await queryDispatcher.HandleAsync<GetSpanEquipmentSpecifications,
                       Result<LookupCollection<SpanEquipmentSpecification>>>(new GetSpanEquipmentSpecifications());

                   return queryResult.Value[context.Source.SpecificationId];
               });

            Field<ManufacturerType>("manufacturer")
                .Description("The manufacturer of the span equipment")
                .ResolveAsync(async context =>
                {
                    if (context.Source.ManufacturerId == null || context.Source.ManufacturerId == Guid.Empty)
                        return null;

                    var queryResult = await queryDispatcher.HandleAsync<GetManufacturer, Result<LookupCollection<Manufacturer>>>(new GetManufacturer());

                    return queryResult.Value[context.Source.ManufacturerId.Value];
                });

            Field<ListGraphType<IdGraphType>>("routeSegmentIds")
                .Description("The route network walk of the span equipment")
                .ResolveAsync(async context =>
                {
                    var queryResult = await queryDispatcher.HandleAsync<GetRouteNetworkDetails, OpenFTTH.Results.Result<GetRouteNetworkDetailsResult>>(
                        new GetRouteNetworkDetails(new InterestIdList() { context.Source.WalkOfInterestId })
                        {
                            RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
                        }
                    );

                    if (queryResult.IsFailed)
                    {
                        throw new ApplicationException($"Got error querying interest information for span equipment: {context.Source.Id} ERROR: " + queryResult.Errors.First().Message);
                    }

                    if (queryResult.Value.Interests == null || !queryResult.Value.Interests.ContainsKey(context.Source.WalkOfInterestId))
                        throw new ApplicationException($"Got no info querying interest information for span equipment: {context.Source.Id}");

                    var routeNetworkElementIds = queryResult.Value.Interests[context.Source.WalkOfInterestId].RouteNetworkElementRefs;

                    List<Guid> segmentIds = new List<Guid>();

                    for (int i = 1; i < routeNetworkElementIds.Count; i += 2)
                        segmentIds.Add(routeNetworkElementIds[i]);

                    return segmentIds;
                });
        }
    }
}
