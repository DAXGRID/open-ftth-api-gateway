using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.QueryHandling
{
    public class GetSpanStructureSpecificationsQueryHandler
        : IQueryHandler<GetSpanStructureSpecifications, Result<LookupCollection<SpanStructureSpecification>>>
    {
        private readonly IEventStore _eventStore;        

        public GetSpanStructureSpecificationsQueryHandler(IEventStore eventStore)
        {
            _eventStore = eventStore;
        }

        public Task<Result<LookupCollection<SpanStructureSpecification>>> HandleAsync(GetSpanStructureSpecifications query)
        {
            var spanStructureSpecificationsProjection = _eventStore.Projections.Get<SpanStructureSpecificationsProjection>();

            return Task.FromResult(
                Result.Ok<LookupCollection<SpanStructureSpecification>>(
                    spanStructureSpecificationsProjection.Specifications
                )
            );
        }
    }
}
