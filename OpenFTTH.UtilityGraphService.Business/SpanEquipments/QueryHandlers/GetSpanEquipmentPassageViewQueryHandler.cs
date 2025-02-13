using DAX.ObjectVersioning.Graph;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.NodeContainers.Projections;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.QueryHandlers.PassageView;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Projections;
using OpenFTTH.UtilityGraphService.Business.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.QueryHandling
{
    public class GetSpanEquipmentPassageViewQueryHandler
        : IQueryHandler<GetSpanEquipmentPassageView, Result<SpanEquipmentPassageViewModel>>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly UtilityNetworkProjection _utilityNetwork;
        private LookupCollection<SpanStructureSpecification> _spanStructureSpecifications;
        private LookupCollection<SpanEquipmentSpecification> _spanEquipmentSpecifications;

        public GetSpanEquipmentPassageViewQueryHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
        }

        public Task<Result<SpanEquipmentPassageViewModel>> HandleAsync(GetSpanEquipmentPassageView query)
        {
            _spanStructureSpecifications = _eventStore.Projections.Get<SpanStructureSpecificationsProjection>().Specifications;
            _spanEquipmentSpecifications = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;

            List<SpanEquipmentPassageViewEquipmentInfo> spanEquipmentViewInfos = new();


            foreach (var spanEquipmentOrSegmentId in query.SpanEquipmentOrSegmentIds)
            {
                if (_utilityNetwork.TryGetEquipment<SpanEquipment>(spanEquipmentOrSegmentId, out var spanEquipment))
                {
                    if (spanEquipment.IsCable)
                    {
                        var cableViewBuilder = new CablePassageViewBuilder(_eventStore, _utilityNetwork, _queryDispatcher, query.RouteNetworkElementId, spanEquipment);
                        spanEquipmentViewInfos.Add(cableViewBuilder.GetCablePassageView());
                    }
                    else
                    {
                        var conduitViewBuilder = new ConduitPassageViewBuilder(_eventStore, _utilityNetwork, _queryDispatcher, query.RouteNetworkElementId, spanEquipment, spanEquipmentOrSegmentId);
                        spanEquipmentViewInfos.Add(conduitViewBuilder.GetConduitPassageView());
                    }
                }
                else
                {
                    return Task.FromResult(Result.Fail<SpanEquipmentPassageViewModel>(new GetEquipmentDetailsError(GetEquipmentDetailsErrorCodes.INVALID_QUERY_ARGUMENT_ERROR_LOOKING_UP_SPECIFIED_EQUIPMENT_BY_EQUIPMENT_ID, $"Invalid query. Cannot find any span equipment by the equipment or span segment id specified: {spanEquipmentOrSegmentId}")));
                }
            }

            return Task.FromResult(
                Result.Ok(
                    new SpanEquipmentPassageViewModel(
                            spanEquipments: spanEquipmentViewInfos.ToArray()
                    )
                )
            );
        }
    }
}
