using DAX.EventProcessing;
using FluentAssertions;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.TestData;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.Business.Graph;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions.Ordering;

#nullable disable

namespace OpenFTTH.UtilityGraphService.Tests.UtilityNetwork
{
    [Order(2000)]
    public class T2000_SpanEquipmentTraceTests
    {
        private readonly IEventStore _eventStore;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly FakeExternalEventProducer _externalEventProducer;

        public T2000_SpanEquipmentTraceTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = (FakeExternalEventProducer)externalEventProducer;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public async Task TestTrace_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipment = TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10;

            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new EquipmentIdList() { sutSpanEquipment })
                {
                    EquipmentDetailsFilter = new EquipmentDetailsFilterOptions()
                    {
                        IncludeRouteNetworkTrace = true
                    }
                }
            );

            // Assert
            equipmentQueryResult.IsSuccess.Should().BeTrue();

            //equipmentQueryResult.Value.RouteNetworkTraces.Should().NotBeNull();

        }

        [Fact, Order(2)]
        public async Task SDU1Trace_ShouldReturnCorrectAddressInfo()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipment = TestUtilityNetwork.CustomerConduit_CC_1_to_SDU_1;

            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new EquipmentIdList() { sutSpanEquipment })
                {
                    EquipmentDetailsFilter = new EquipmentDetailsFilterOptions()
                    {
                        IncludeRouteNetworkTrace = true
                    }
                }
            );

            // Assert
            equipmentQueryResult.IsSuccess.Should().BeTrue();

            equipmentQueryResult.Value.RouteNetworkTraces.Should().NotBeNull();

            equipmentQueryResult.Value.RouteNetworkTraces.First().ToRouteNodeName.Should().StartWith("Engum");
        }

        [Fact, Order(2)]
        public async Task SDU2Trace_ShouldReturnCorrectAddressInfo()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipment = TestUtilityNetwork.CustomerConduit_CC_1_to_SDU_2;

            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new EquipmentIdList() { sutSpanEquipment })
                {
                    EquipmentDetailsFilter = new EquipmentDetailsFilterOptions()
                    {
                        IncludeRouteNetworkTrace = true
                    }
                }
            );

            // Assert
            equipmentQueryResult.IsSuccess.Should().BeTrue();

            equipmentQueryResult.Value.RouteNetworkTraces.Should().NotBeNull();

            equipmentQueryResult.Value.RouteNetworkTraces.First().ToRouteNodeName.Should().StartWith("Vesterbrogade");
        }

        [Fact, Order(3)]
        public async Task SDU3Trace_ShouldReturnCorrectAddressInfo()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipment = TestUtilityNetwork.CustomerConduit_CC_1_to_SDU_3;

            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new EquipmentIdList() { sutSpanEquipment })
                {
                    EquipmentDetailsFilter = new EquipmentDetailsFilterOptions()
                    {
                        IncludeRouteNetworkTrace = true
                    }
                }
            );

            // Assert
            equipmentQueryResult.IsSuccess.Should().BeTrue();

            equipmentQueryResult.Value.RouteNetworkTraces.Should().NotBeNull();

            equipmentQueryResult.Value.RouteNetworkTraces.First().ToRouteNodeName.Should().Contain("SDU 3");
        }

    }
}

#nullable enable
