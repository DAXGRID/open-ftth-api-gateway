using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.RouteNetwork.API.Model;
using System;

namespace OpenFTTH.RouteNetwork.API.Commands
{

    /// <summary>
    /// Used to register a walk of interest in the route network.
    /// A walk is a sequence of edges (route segments) which joins a sequence of vertices (route nodes).
    /// An typical usage is to register some equipment spanning one or more route segments, such as a conduit or cable.
    /// You can specify the walk by providing route node ids, route segment ids or both.
    /// The result will always contain all the route network element ids.
    /// </summary>
    public record RegisterWalkOfInterest : BaseCommand, ICommand<Result<RouteNetworkInterest>>
    {
        public static string RequestName => typeof(RegisterWalkOfInterest).Name;
        public Guid InterestId { get; }
        public RouteNetworkElementIdList WalkIds { get; }
        public string? CustomData { get; }

        /// <summary>
        /// </summary>
        /// <param name="interestId">Client specified uuid</param>
        /// <param name="walk">
        /// A walk in the route network graph. Can be specified using node ids or segments ids or both.
        /// </param>
        /// <param name="customData">Optional extra data to be save in the interest record</param>
        public RegisterWalkOfInterest(Guid correlationId, UserContext userContext, Guid interestId, RouteNetworkElementIdList walk, string? customData = null) : base(correlationId, userContext)
        {
            this.InterestId = interestId;
            this.WalkIds = walk;
            this.CustomData = customData;
        }
    }
}
