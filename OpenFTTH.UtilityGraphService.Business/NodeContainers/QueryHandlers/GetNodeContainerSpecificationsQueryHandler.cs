using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.Business.NodeContainers.Projections;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.NodeEquipments.QueryHandling
{
    public class GetNodeContainerSpecificationsQueryHandler
        : IQueryHandler<GetNodeContainerSpecifications, Result<LookupCollection<NodeContainerSpecification>>>
    {
        private readonly IEventStore _eventStore;        

        public GetNodeContainerSpecificationsQueryHandler(IEventStore eventStore)
        {
            _eventStore = eventStore;
        }

        public Task<Result<LookupCollection<NodeContainerSpecification>>> HandleAsync(GetNodeContainerSpecifications query)
        {
            var nodeContainerSpecificationsProjection = _eventStore.Projections.Get<NodeContainerSpecificationsProjection>();

            return Task.FromResult(
                Result.Ok<LookupCollection<NodeContainerSpecification>>(
                    nodeContainerSpecificationsProjection.Specifications
                )
            );
        }
    }
}
