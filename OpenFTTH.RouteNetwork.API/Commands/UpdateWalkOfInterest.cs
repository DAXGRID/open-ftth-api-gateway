using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.RouteNetwork.API.Model;
using System;

namespace OpenFTTH.RouteNetwork.API.Commands
{

    public record UpdateWalkOfInterest : BaseCommand, ICommand<Result<RouteNetworkInterest>>
    {
        public static string RequestName => typeof(RegisterWalkOfInterest).Name;
        public Guid InterestId { get; }
        public RouteNetworkElementIdList WalkIds { get; }
        public string? CustomData { get; }

        public UpdateWalkOfInterest(Guid correlationId, UserContext userContext, Guid interestId, RouteNetworkElementIdList walk, string? customData = null) : base(correlationId, userContext)
        {
            this.InterestId = interestId;
            this.WalkIds = walk;
            this.CustomData = customData;
        }
    }
}
