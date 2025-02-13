using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.RouteNetwork.API.Model;
using System;

namespace OpenFTTH.RouteNetwork.API.Commands
{

    /// <summary>
    /// Used to validate a walk of interest in the route network.
    /// A walk is a sequence of edges (route segments) which joins a sequence of vertices (route nodes).
    /// </summary>
    public record ValidateWalkOfInterest : BaseCommand, ICommand<Result<ValidatedRouteNetworkWalk>>
    {
        public static string RequestName => typeof(RegisterWalkOfInterest).Name;
        public RouteNetworkElementIdList WalkIds { get; }

        /// <summary>
        /// </summary>
        /// <param name="segmentWalk">
        /// A walk in the route network graph specified using segments ids.
        /// </param>
        public ValidateWalkOfInterest(Guid correlationId, UserContext userContext, RouteNetworkElementIdList segmentWalk) : base(correlationId, userContext)
        {
            this.WalkIds = segmentWalk;
        }
    }
}
