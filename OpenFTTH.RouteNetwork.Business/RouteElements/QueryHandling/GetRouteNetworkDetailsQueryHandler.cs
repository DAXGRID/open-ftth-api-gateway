using FluentResults;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.RouteNetwork.Business.Interest.Projections;
using OpenFTTH.RouteNetwork.Business.RouteElements.Model;
using OpenFTTH.RouteNetwork.Business.RouteElements.StateHandling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.RouteNetwork.Business.RouteElements.QueryHandlers
{
    public class GetRouteNetworkDetailsQueryHandler :
        IQueryHandler<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>
    {
        private readonly ILogger<GetRouteNetworkDetailsQueryHandler> _logger;
        private readonly IEventStore _eventStore;
        private readonly IRouteNetworkRepository _routeNodeRepository;

        public GetRouteNetworkDetailsQueryHandler(ILoggerFactory loggerFactory, IEventStore eventStore, IRouteNetworkRepository routeNodeRepository)
        {
            if (null == loggerFactory)
            {
                throw new ArgumentNullException("loggerFactory is null");
            }

            _logger = loggerFactory.CreateLogger<GetRouteNetworkDetailsQueryHandler>();

            _eventStore = eventStore;
            _routeNodeRepository = routeNodeRepository;
        }

        public Task<Result<GetRouteNetworkDetailsResult>> HandleAsync(GetRouteNetworkDetails query)
        {
            // Get route elements
            if (query.RouteNetworkElementIdsToQuery.Count > 0 && query.InterestIdsToQuery.Count == 0)
            {
                return QueryByRouteElementIds(query);
            }
            else if (query.InterestIdsToQuery.Count > 0 && query.RouteNetworkElementIdsToQuery.Count == 0)
            {
                return QueryByInterestIds(query);
            }
            else
            {
                if (query.InterestIdsToQuery.Count > 0 && query.RouteNetworkElementIdsToQuery.Count > 0)
                    return Task.FromResult(Result.Fail<GetRouteNetworkDetailsResult>(new GetRouteNetworkDetailsError(GetRouteNetworkDetailsErrorCodes.INVALID_QUERY_ARGUMENT_CANT_QUERY_BY_INTEREST_AND_ROUTE_NETWORK_ELEMENT_AT_THE_SAME_TIME, "Invalid query. Cannot query by route network element ids and interest ids at the same time.")));
                else if (query.InterestIdsToQuery.Count == 0 && query.RouteNetworkElementIdsToQuery.Count == 0)
                    return Task.FromResult(Result.Fail<GetRouteNetworkDetailsResult>(new GetRouteNetworkDetailsError(GetRouteNetworkDetailsErrorCodes.INVALID_QUERY_ARGUMENT_NO_INTEREST_OR_ROUTE_NETWORK_IDS_SPECIFIED, "Invalid query. Neither route network element ids or interest ids specified. Therefore nothing to query.")));
                else
                    throw new ApplicationException("Unexpected combination of query arguments in GetRouteNetworkDetailsResult:\r\n" + JsonConvert.SerializeObject(query));
            }
        }

        private Task<Result<GetRouteNetworkDetailsResult>> QueryByInterestIds(GetRouteNetworkDetails query)
        {
            RouteNetworkElementIdList routeElementsToQuery = new RouteNetworkElementIdList();

            List<RouteNetworkInterest> interestsToReturn = new List<RouteNetworkInterest>();

            // Find all interest to return and create a list of route network elements at the same time
            var interestsProjection = _eventStore.Projections.Get<InterestsProjection>();

            foreach (var interestId in query.InterestIdsToQuery)
            {
                var interestQueryResult = interestsProjection.GetInterest(interestId);

                // Here we return a error result, because we're dealing with invalid interest ids provided by the client
                if (interestQueryResult.IsFailed)
                    return Task.FromResult(
                        Result.Fail<GetRouteNetworkDetailsResult>(new GetRouteNetworkDetailsError(GetRouteNetworkDetailsErrorCodes.INVALID_QUERY_ARGUMENT_ERROR_LOOKING_UP_SPECIFIED_INTEREST_BY_ID, $"Error looking up interest by id: {interestId}")).
                        WithError(interestQueryResult.Errors.First())
                    );

                interestsToReturn.Add(interestQueryResult.Value);

                routeElementsToQuery.AddRange(interestQueryResult.Value.RouteNetworkElementRefs);
            }

            // TODO: Fix so that we don't create substitudes when something is deleted
            var getRouteNetworkElementsResult = _routeNodeRepository.GetRouteElements(routeElementsToQuery, true);

            // Here we create an exception, because this situation should not be allowed by the system
            if (getRouteNetworkElementsResult.IsFailed)
                throw new ApplicationException($"Unexpected error querying route elements referenced by interests. All the interest exists and therefore the route network elements must also exists. Validation that route elements having interest cannot be deleted seems not to be working! Initial query:\r\n" + JsonConvert.SerializeObject(query));

            var mappedRouteNetworkElements = MapRouteElementDomainObjectsToQueryObjects(query.RouteNetworkElementFilter, getRouteNetworkElementsResult.Value);

            var queryResult = new GetRouteNetworkDetailsResult(mappedRouteNetworkElements, interestsToReturn.ToArray());

            // Add interest reference information
            AddInterestReferencesToRouteNetworkElements(query, queryResult);

            return Task.FromResult(
                Result.Ok<GetRouteNetworkDetailsResult>(
                    queryResult
                )
            );
        }


        private Task<Result<GetRouteNetworkDetailsResult>> QueryByRouteElementIds(GetRouteNetworkDetails query)
        {
            var getRouteNetworkElementsResult = _routeNodeRepository.GetRouteElements(query.RouteNetworkElementIdsToQuery);

            // Here we return a error result, because we're dealing with invalid route network ids provided by the client
            if (getRouteNetworkElementsResult.IsFailed)
                    return Task.FromResult(
                        Result.Fail<GetRouteNetworkDetailsResult>(new GetRouteNetworkDetailsError(GetRouteNetworkDetailsErrorCodes.INVALID_QUERY_ARGUMENT_ERROR_LOOKING_UP_SPECIFIED_ROUTE_NETWORK_ELEMENT_BY_ID, $"Error looking up route network elements: " + JsonConvert.SerializeObject(query))).
                        WithError(getRouteNetworkElementsResult.Errors.First())
                    );

            var routeNetworkElementsToReturn = MapRouteElementDomainObjectsToQueryObjects(query.RouteNetworkElementFilter, getRouteNetworkElementsResult.Value);

            var interestsToReturn = Array.Empty<RouteNetworkInterest>();

            if (query.RelatedInterestFilter == RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects)
            {
                interestsToReturn = GetInterestsRelatedToRouteNetworkElements(query.RouteNetworkElementIdsToQuery);
            }

            var queryResult = new GetRouteNetworkDetailsResult(routeNetworkElementsToReturn, interestsToReturn);

            // Add interest information
            AddInterestReferencesToRouteNetworkElements(query, queryResult);

            return Task.FromResult(
                Result.Ok<GetRouteNetworkDetailsResult>(
                    queryResult
                )
            );
        }

        private void AddInterestReferencesToRouteNetworkElements(GetRouteNetworkDetails query, GetRouteNetworkDetailsResult queryResult)
        {
            if (query.RelatedInterestFilter != RelatedInterestFilterOptions.None)
            {
                var interestsProjection = _eventStore.Projections.Get<InterestsProjection>();

                foreach (var routeElement in queryResult.RouteNetworkElements)
                {
                    // Add relations to the route network element
                    var interestRelationsResult = interestsProjection.GetInterestsByRouteNetworkElementId(routeElement.Id);

                    if (interestRelationsResult.IsFailed)
                        throw new ApplicationException($"Unexpected error querying interests related to route network element with id: {routeElement.Id} {interestRelationsResult.Errors.First()}");

                    routeElement.InterestRelations = MapInterestRelationDomainObjectsToQueryObjects(interestRelationsResult.Value);
                }
            }
        }

        private RouteNetworkInterest[] GetInterestsRelatedToRouteNetworkElements(RouteNetworkElementIdList routeNetworkElementIds)
        {
            Dictionary<Guid, RouteNetworkInterest> interestsToBeAddedToResult = new Dictionary<Guid, RouteNetworkInterest>();

            var interestsProjection = _eventStore.Projections.Get<InterestsProjection>();

            foreach (var routeElementId in routeNetworkElementIds)
            {
                // Add relations to the route network element
                var interestRelationsResult = interestsProjection.GetInterestsByRouteNetworkElementId(routeElementId);

                if (interestRelationsResult.IsFailed)
                    throw new ApplicationException($"Unexpected error querying interests related to route network element with id: {routeElementId} {interestRelationsResult.Errors.First()}");

                foreach (var interestRelation in interestRelationsResult.Value)
                {
                    if (!interestsToBeAddedToResult.ContainsKey(interestRelation.Item1.Id))
                    {
                        interestsToBeAddedToResult.Add(interestRelation.Item1.Id, interestRelation.Item1);
                    }
                }
            }

            return interestsToBeAddedToResult.Values.ToArray();
        }

        public static RouteNetworkElement[] MapRouteElementDomainObjectsToQueryObjects(RouteNetworkElementFilterOptions filter, List<IRouteNetworkElement> routeNetworkElements)
        {
            var routeNetworkElementDTOs = new List<RouteNetworkElement>();

            foreach (var routeNetworkElement in routeNetworkElements)
            {
                RouteNetworkElementKindEnum kind = (routeNetworkElement is IRouteNode) ? RouteNetworkElementKindEnum.RouteNode : RouteNetworkElementKindEnum.RouteSegment;

                routeNetworkElementDTOs.Add(
                    new RouteNetworkElement(routeNetworkElement.Id, kind)
                    {
                        Coordinates = filter.IncludeCoordinates ? routeNetworkElement.Coordinates : null,
                        RouteSegmentInfo = filter.IncludeRouteSegmentInfo && routeNetworkElement is IRouteSegment segment ? segment.RouteSegmentInfo : null,
                        RouteNodeInfo = filter.IncludeRouteNodeInfo && routeNetworkElement is IRouteNode node ? node.RouteNodeInfo : null,
                        NamingInfo = filter.IncludeNamingInfo ? routeNetworkElement.NamingInfo : null,
                        MappingInfo = filter.IncludeMappingInfo ? routeNetworkElement.MappingInfo : null,
                        LifecycleInfo = filter.IncludeLifecycleInfo ? routeNetworkElement.LifecycleInfo : null,
                        SafetyInfo = filter.IncludeSafetyInfo ? routeNetworkElement.SafetyInfo : null,
                    }
                );
            }

            return routeNetworkElementDTOs.ToArray();
        }

        private static RouteNetworkElementInterestRelation[] MapInterestRelationDomainObjectsToQueryObjects(List<(RouteNetworkInterest, RouteNetworkInterestRelationKindEnum)> interestRelations)
        {
            var interestRelationsToReturn = new List<RouteNetworkElementInterestRelation>();

            foreach (var interestRelation in interestRelations)
            {
                interestRelationsToReturn.Add(
                    new RouteNetworkElementInterestRelation(interestRelation.Item1.Id, interestRelation.Item2)
                );
            }

            return interestRelationsToReturn.ToArray();
        }

    }
}


