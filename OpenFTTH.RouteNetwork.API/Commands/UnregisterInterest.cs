using FluentResults;
using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.RouteNetwork.API.Commands
{
    public record UnregisterInterest : BaseCommand, ICommand<Result>
    {
        public static string RequestName => typeof(RegisterNodeOfInterest).Name;
        public Guid InterestId { get; }

        public UnregisterInterest(Guid correlationId, UserContext userContext, Guid interestId) : base(correlationId, userContext)
        {
            this.InterestId = interestId;
        }
    }
}
