using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.RouteNetwork.API.Model;
using System;

namespace OpenFTTH.RouteNetwork.API.Queries
{
    /// <summary>
    /// Used to query detailed information of one or more route network elements (route nodes and/or segments),
    /// including related interest information if explicitly requested. The query supports fetching information 
    /// by means of a list of route network ids or interest ids.
    /// </summary>
    public class GetRouteNetworkDetails : IQuery<Result<GetRouteNetworkDetailsResult>>
    {
        public static string RequestName => typeof(GetRouteNetworkDetails).Name;

        /// <summary>
        /// List of route network element ids (route nodes and/or route segments) specified by the calling client
        /// </summary>
        public RouteNetworkElementIdList RouteNetworkElementIdsToQuery { get; }

        /// <summary>
        /// List of interest ids (point and/or walk of interests) specified by the calling client
        /// </summary>
        public InterestIdList InterestIdsToQuery { get; }

        #region Related Interest Filter Options
        private RelatedInterestFilterOptions _relatedInterestFilterOptions = RelatedInterestFilterOptions.ReferencesFromRouteElementOnly;

        public RelatedInterestFilterOptions RelatedInterestFilter
        {
            get { return _relatedInterestFilterOptions; }
            init { _relatedInterestFilterOptions = value; }
        }
        #endregion

        #region Route Network Element Filter Options
        private RouteNetworkElementFilterOptions _routeNetworkElementFilterOptions = 
            new RouteNetworkElementFilterOptions()
                {
                    IncludeRouteNodeInfo = true,
                    IncludeRouteSegmentInfo = true,
                    IncludeCoordinates = true,
                    IncludeNamingInfo = true,
                    IncludeMappingInfo = true,
                    IncludeLifecycleInfo = true,
                    IncludeSafetyInfo = true
                };

        public RouteNetworkElementFilterOptions RouteNetworkElementFilter
        {
            get { return _routeNetworkElementFilterOptions; }
            init { _routeNetworkElementFilterOptions = value; }
        }
        #endregion

        /// <summary>
        /// Use this contructor, if you want to query by route network element ids
        /// </summary>
        /// <param name="routeNetworkElementIds"></param>
        public GetRouteNetworkDetails(RouteNetworkElementIdList routeNetworkElementIds)
        {
            // Add empty list to InterestIdsToQuery, because the client want to query by route network element ids
            InterestIdsToQuery = new InterestIdList(); 

            this.RouteNetworkElementIdsToQuery = routeNetworkElementIds;
        }

        /// <summary>
        /// Use this contructor, if you want to query by interest ids
        /// </summary>
        /// <param name="interestIds"></param>
        public GetRouteNetworkDetails(InterestIdList interestIds)
        {
            // Add empty list to RouteNetworkElementIdsToQuery, because the client want to query by interest ids
            RouteNetworkElementIdsToQuery = new RouteNetworkElementIdList();

            if (interestIds == null || interestIds.Count == 0)
                throw new ArgumentException("At least one interest id must be specified using the interestIds parameter");

            this.InterestIdsToQuery = interestIds;
        }

    }
}
