using DAX.EventProcessing;
using FluentAssertions;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
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
    [Order(901)]
    public class T0901_SpanEquipmentConnectByIndexTests
    {
        private readonly IEventStore _eventStore;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly FakeExternalEventProducer _externalEventProducer;

        public T0901_SpanEquipmentConnectByIndexTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = (FakeExternalEventProducer)externalEventProducer;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public async Task TestConnect5x10Sub5ToCustomerConduitByIndexAtCC_1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutConnectFromSpanEquipment = TestUtilityNetwork.MultiConduit_5x10_CC_1_to_SP_1;
            var sutConnectToSpanEquipment = TestUtilityNetwork.CustomerConduit_CC_1_to_SDU_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectFromSpanEquipment, out var sutFromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectToSpanEquipment, out var sutToSpanEquipment);

            // Connect inner conduit 5 in 5x10 to customer conduit
            var connectCmd = new ConnectSpanSegmentsByIndexAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CC_1,
                fromSpanEquipmentId: sutFromSpanEquipment.Id,
                toSpanEquipmentId: sutToSpanEquipment.Id,
                fromStructureIndex: 5,
                toStructureIndex: 0,
                numberOfUnits: 1
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsByIndexAtRouteNode, Result>(connectCmd);

            var fromEquipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
               new GetEquipmentDetails(new EquipmentIdList() { sutConnectFromSpanEquipment })
            );

            var toEquipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
              new GetEquipmentDetails(new EquipmentIdList() { sutConnectToSpanEquipment })
            );

            // Assert
            connectResult.IsSuccess.Should().BeTrue();
            fromEquipmentQueryResult.IsSuccess.Should().BeTrue();
            toEquipmentQueryResult.IsSuccess.Should().BeTrue();

            var fromEquipmentAfterConnect = fromEquipmentQueryResult.Value.SpanEquipment[sutConnectFromSpanEquipment];
            fromEquipmentAfterConnect.SpanStructures[5].SpanSegments[0].FromTerminalId.Should().NotBeEmpty();

            var terminalId = fromEquipmentAfterConnect.SpanStructures[5].SpanSegments[0].FromTerminalId;

            var toEquipmentAfterConnect = toEquipmentQueryResult.Value.SpanEquipment[sutConnectToSpanEquipment];
            toEquipmentAfterConnect.SpanStructures[0].SpanSegments[0].FromTerminalId.Should().Be(terminalId);
        }


        [Fact, Order(2)]
        public async Task TestConnect5x10Sub1_2_3To5x10IndexAtCC_1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutConnectFromSpanEquipment = TestUtilityNetwork.MultiConduit_5x10_CC_1_to_SP_1;
            var sutConnectToSpanEquipment = TestUtilityNetwork.MultiConduit_5x10_CC_1_to_HH_11;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectFromSpanEquipment, out var sutFromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectToSpanEquipment, out var sutToSpanEquipment);

            // Connect inner conduit 1,2,3 in 5x10 to the other 5x10
            var connectCmd = new ConnectSpanSegmentsByIndexAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CC_1,
                fromSpanEquipmentId: sutFromSpanEquipment.Id,
                toSpanEquipmentId: sutToSpanEquipment.Id,
                fromStructureIndex: 1,
                toStructureIndex: 3,
                numberOfUnits: 3
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsByIndexAtRouteNode, Result>(connectCmd);

            var fromEquipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
               new GetEquipmentDetails(new EquipmentIdList() { sutConnectFromSpanEquipment })
            );

            var toEquipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
              new GetEquipmentDetails(new EquipmentIdList() { sutConnectToSpanEquipment })
            );

            // Assert
            connectResult.IsSuccess.Should().BeTrue();
            fromEquipmentQueryResult.IsSuccess.Should().BeTrue();
            toEquipmentQueryResult.IsSuccess.Should().BeTrue();

            var fromEquipmentAfterConnect = fromEquipmentQueryResult.Value.SpanEquipment[sutConnectFromSpanEquipment];
            var toEquipmentAfterConnect = toEquipmentQueryResult.Value.SpanEquipment[sutConnectToSpanEquipment];


            // check from inner 1 to inner 3
            fromEquipmentAfterConnect.SpanStructures[1].SpanSegments[0].FromTerminalId.Should().NotBeEmpty();
            var terminalId1 = fromEquipmentAfterConnect.SpanStructures[1].SpanSegments[0].FromTerminalId;
            toEquipmentAfterConnect.SpanStructures[3].SpanSegments[0].FromTerminalId.Should().Be(terminalId1);

            // check from inner 2 to inner 4
            fromEquipmentAfterConnect.SpanStructures[2].SpanSegments[0].FromTerminalId.Should().NotBeEmpty();
            var terminalId2 = fromEquipmentAfterConnect.SpanStructures[2].SpanSegments[0].FromTerminalId;
            toEquipmentAfterConnect.SpanStructures[4].SpanSegments[0].FromTerminalId.Should().Be(terminalId2);

            // check from inner 3 to inner 5
            fromEquipmentAfterConnect.SpanStructures[3].SpanSegments[0].FromTerminalId.Should().NotBeEmpty();
            var terminalId3 = fromEquipmentAfterConnect.SpanStructures[3].SpanSegments[0].FromTerminalId;
            toEquipmentAfterConnect.SpanStructures[5].SpanSegments[0].FromTerminalId.Should().Be(terminalId3);

        }

        [Fact, Order(10)]
        public async Task TestConnectAlreadyConnected_ShouldFail()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutConnectFromSpanEquipment = TestUtilityNetwork.MultiConduit_5x10_CC_1_to_SP_1;
            var sutConnectToSpanEquipment = TestUtilityNetwork.CustomerConduit_CC_1_to_SDU_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectFromSpanEquipment, out var sutFromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectToSpanEquipment, out var sutToSpanEquipment);

            // Connect inner conduit 5 in 5x10 to customer conduit
            var connectCmd = new ConnectSpanSegmentsByIndexAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CC_1,
                fromSpanEquipmentId: sutFromSpanEquipment.Id,
                toSpanEquipmentId: sutToSpanEquipment.Id,
                fromStructureIndex: 5,
                toStructureIndex: 0,
                numberOfUnits: 1
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsByIndexAtRouteNode, Result>(connectCmd);

            // Assert
            connectResult.IsFailed.Should().BeTrue();
        }

        [Fact, Order(11)]
        public async Task TestOutOfBoundsIndex_ShouldFail()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutConnectFromSpanEquipment = TestUtilityNetwork.MultiConduit_5x10_CC_1_to_SP_1;
            var sutConnectToSpanEquipment = TestUtilityNetwork.MultiConduit_5x10_CC_1_to_HH_11;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectFromSpanEquipment, out var sutFromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectToSpanEquipment, out var sutToSpanEquipment);

            // Connect inner conduit 4,5,6 in 5x10 to the other 5x10. 6 don't exist!!!
            var connectCmd = new ConnectSpanSegmentsByIndexAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CC_1,
                fromSpanEquipmentId: sutFromSpanEquipment.Id,
                toSpanEquipmentId: sutToSpanEquipment.Id,
                fromStructureIndex: 4,
                toStructureIndex: 3,
                numberOfUnits: 3
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsByIndexAtRouteNode, Result>(connectCmd);

            // Assert
            connectResult.IsFailed.Should().BeTrue();
        }

    }
}

#nullable enable
