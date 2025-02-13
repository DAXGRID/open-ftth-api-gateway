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
    public class RegisterNodeOfInterestCommandHandler : ICommandHandler<RegisterNodeOfInterest, Result<RouteNetworkInterest>>
    {
        private readonly IEventStore _eventStore;
        private readonly IRouteNetworkRepository _routeNetworkRepository;

        public RegisterNodeOfInterestCommandHandler(IEventStore eventStore, IRouteNetworkRepository routeNodeRepository)
        {
            _eventStore = eventStore;
            _routeNetworkRepository = routeNodeRepository;
        }

        public Task<Result<RouteNetworkInterest>> HandleAsync(RegisterNodeOfInterest command)
        {
            var interestProjection = _eventStore.Projections.Get<InterestsProjection>();

            var nodeOfInterest = new RouteNetworkInterest(command.InterestId, RouteNetworkInterestKindEnum.NodeOfInterest, new RouteNetworkElementIdList() { command.RouteNetworkElementId });

            var interestAR = new InterestAR();

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            var registerInterestResult = interestAR.RegisterNodeOfInterest(commandContext, nodeOfInterest, interestProjection);

            if (registerInterestResult.IsSuccess)
            {
                _eventStore.Aggregates.Store(interestAR);
            }

            return Task.FromResult(registerInterestResult);
        }
    }
}
