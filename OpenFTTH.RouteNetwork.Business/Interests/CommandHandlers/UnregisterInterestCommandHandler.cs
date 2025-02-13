using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.Business.Interest;
using OpenFTTH.RouteNetwork.Business.Interest.Projections;
using OpenFTTH.RouteNetwork.Business.RouteElements.StateHandling;
using System.Threading.Tasks;

namespace OpenFTTH.RouteNetwork.Business.MutationHandling
{
    public class UnregisterInterestCommandHandler : ICommandHandler<UnregisterInterest, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IRouteNetworkRepository _routeNetworkRepository;

        public UnregisterInterestCommandHandler(IEventStore eventStore, IRouteNetworkRepository routeNodeRepository)
        {
            _eventStore = eventStore;
            _routeNetworkRepository = routeNodeRepository;
        }

        public Task<Result> HandleAsync(UnregisterInterest command)
        {
            var interestProjection = _eventStore.Projections.Get<InterestsProjection>();

            var interestAR = _eventStore.Aggregates.Load<InterestAR>(command.InterestId);

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            var unregisterInterestResult = interestAR.UnregisterInterest(commandContext, interestProjection, command.InterestId);

            if (unregisterInterestResult.IsSuccess)
            {
                _eventStore.Aggregates.Store(interestAR);
            }

            return Task.FromResult(unregisterInterestResult);
        }
    }
}
