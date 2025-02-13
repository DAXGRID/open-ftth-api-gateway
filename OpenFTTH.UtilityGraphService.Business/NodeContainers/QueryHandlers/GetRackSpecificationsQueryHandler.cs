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
    public class GetRackSpecificationsQueryHandler
        : IQueryHandler<GetRackSpecifications, Result<LookupCollection<RackSpecification>>>
    {
        private readonly IEventStore _eventStore;        

        public GetRackSpecificationsQueryHandler(IEventStore eventStore)
        {
            _eventStore = eventStore;
        }

        public Task<Result<LookupCollection<RackSpecification>>> HandleAsync(GetRackSpecifications query)
        {
            var rackSpecificationsProjection = _eventStore.Projections.Get<RackSpecificationsProjection>();

            return Task.FromResult(
                Result.Ok<LookupCollection<RackSpecification>>(
                    rackSpecificationsProjection.Specifications
                )
            );
        }
    }
}
