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
    [Order(900)]
    public class T0900_SpanEquipmentConnectTests
    {
        private readonly IEventStore _eventStore;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly FakeExternalEventProducer _externalEventProducer;

        public T0900_SpanEquipmentConnectTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = (FakeExternalEventProducer)externalEventProducer;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public async Task TestConnect6x10InnerConduit4To3x10AtCC_1_ShouldSucceed()
        {
            MakeSureTestConduitIsCutAtCC_1();

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutConnectFromSpanEquipmentId = TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10;
            var sutConnectToSpanEquipmentId = TestUtilityNetwork.MultiConduit_3x10_CC_1_to_SP_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectFromSpanEquipmentId, out var sutFromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectToSpanEquipmentId, out var sutToSpanEquipment);

            // Connect inner conduit 4 in 6x10 with inner conduit 3 in 3x10
            var connectCmd = new ConnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CC_1,
                spanSegmentsToConnect: new Guid[] {
                    sutFromSpanEquipment.SpanStructures[4].SpanSegments[0].Id,
                    sutToSpanEquipment.SpanStructures[3].SpanSegments[0].Id
                }
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd);

            var fromEquipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
               new GetEquipmentDetails(new EquipmentIdList() { sutConnectFromSpanEquipmentId })
            );

            var toEquipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
              new GetEquipmentDetails(new EquipmentIdList() { sutConnectToSpanEquipmentId })
            );

            // Assert
            connectResult.IsSuccess.Should().BeTrue();
            fromEquipmentQueryResult.IsSuccess.Should().BeTrue();
            toEquipmentQueryResult.IsSuccess.Should().BeTrue();

            var fromEquipmentAfterConnect = fromEquipmentQueryResult.Value.SpanEquipment[sutConnectFromSpanEquipmentId];
            fromEquipmentAfterConnect.SpanStructures[4].SpanSegments[0].ToTerminalId.Should().NotBeEmpty();
            
            var terminalId = fromEquipmentAfterConnect.SpanStructures[4].SpanSegments[0].ToTerminalId;

            var toEquipmentAfterConnect = toEquipmentQueryResult.Value.SpanEquipment[sutConnectToSpanEquipmentId];
            toEquipmentAfterConnect.SpanStructures[3].SpanSegments[0].FromTerminalId.Should().Be(terminalId);
        }

        [Fact, Order(2)]
        public async Task TestConnect6x10InnerConduit3To5x10AtCC_1_ShouldSucceed()
        {
            MakeSureTestConduitIsCutAtCC_1();

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutConnectFromSpanEquipmentId = TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10;
            var sutConnectToSpanEquipmentId = TestUtilityNetwork.MultiConduit_5x10_CC_1_to_SP_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectFromSpanEquipmentId, out var sutFromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectToSpanEquipmentId, out var sutToSpanEquipment);

            // Connect inner conduit 3 in 6x10 with inner conduit 4 in 5x10
            var connectCmd = new ConnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CC_1,
                spanSegmentsToConnect: new Guid[] {
                    sutFromSpanEquipment.SpanStructures[3].SpanSegments[0].Id,
                    sutToSpanEquipment.SpanStructures[4].SpanSegments[0].Id,
                }
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd);

            var fromEquipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
               new GetEquipmentDetails(new EquipmentIdList() { sutConnectFromSpanEquipmentId })
            );

            var toEquipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
              new GetEquipmentDetails(new EquipmentIdList() { sutConnectToSpanEquipmentId })
            );

            // Assert
            connectResult.IsSuccess.Should().BeTrue();
        }

        [Fact, Order(11)]
        public async Task TestConnectMultipleInnerducts5x10To3x10ConduitAtCC_1_ShouldSucceed()
        {
            MakeSureTestConduitIsCutAtCC_1();

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutConnectFromSpanEquipment = TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10;
            var sutConnectToSpanEquipment = TestUtilityNetwork.MultiConduit_3x10_CC_1_to_SP_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectFromSpanEquipment, out var sutFromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectToSpanEquipment, out var sutToSpanEquipment);

            // Connect two inner conduits in 5x10 with inner conduits in 3x10
            var connectCmd = new ConnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CC_1,
                spanSegmentsToConnect: new Guid[] {
                    sutFromSpanEquipment.SpanStructures[1].SpanSegments[0].Id,
                    sutToSpanEquipment.SpanStructures[1].SpanSegments[0].Id,
                    sutFromSpanEquipment.SpanStructures[2].SpanSegments[0].Id,
                    sutToSpanEquipment.SpanStructures[2].SpanSegments[0].Id
                }
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd);

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

            var connection1fromSegment = fromEquipmentAfterConnect.SpanStructures[1].SpanSegments[0];
            var connection1toSegment = toEquipmentAfterConnect.SpanStructures[1].SpanSegments[0];

            var connection2fromSegment = fromEquipmentAfterConnect.SpanStructures[2].SpanSegments[0];
            var connection2toSegment = toEquipmentAfterConnect.SpanStructures[2].SpanSegments[0];


            // First connection
            connection1fromSegment.ToTerminalId.Should().NotBeEmpty();
            connection1toSegment.FromTerminalId.Should().Be(connection1fromSegment.ToTerminalId);

            // Second connection
            connection2fromSegment.ToTerminalId.Should().NotBeEmpty();
            connection2toSegment.FromTerminalId.Should().Be(connection2fromSegment.ToTerminalId);
        }

        [Fact, Order(12)]
        public async Task TestConnectAlreadyConnectedSegment_ShouldFail()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutConnectFromSpanEquipment = TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10;
            var sutConnectToSpanEquipment = TestUtilityNetwork.MultiConduit_3x10_CC_1_to_SP_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectFromSpanEquipment, out var sutFromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectToSpanEquipment, out var sutToSpanEquipment);

            // Connect inner conduit 2 in 5x10 with inner conduit 1 in 3x10
            var connectCmd = new ConnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CC_1,
                spanSegmentsToConnect: new Guid[] {
                    sutFromSpanEquipment.SpanStructures[2].SpanSegments[0].Id,
                    sutToSpanEquipment.SpanStructures[1].SpanSegments[0].Id
                }
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd);

            // Assert
            connectResult.IsFailed.Should().BeTrue();
            ((ConnectSpanSegmentsAtRouteNodeError)connectResult.Errors.First()).Code.Should().Be(ConnectSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_ALREADY_CONNECTED);
        }

        [Fact, Order(13)]
        public async Task TestConnectInnerToOuterConduit_ShouldFail()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutConnectFromSpanEquipment = TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10;
            var sutConnectToSpanEquipment = TestUtilityNetwork.MultiConduit_3x10_CC_1_to_SP_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectFromSpanEquipment, out var sutFromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectToSpanEquipment, out var sutToSpanEquipment);

            // Connect inner conduit 2 in 5x10 with inner conduit 1 in 3x10
            var connectCmd = new ConnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CC_1,
                spanSegmentsToConnect: new Guid[] {
                    sutFromSpanEquipment.SpanStructures[2].SpanSegments[1].Id,
                    sutToSpanEquipment.SpanStructures[0].SpanSegments[0].Id
                }
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd);

            // Assert
            connectResult.IsFailed.Should().BeTrue();
            ((ConnectSpanSegmentsAtRouteNodeError)connectResult.Errors.First()).Code.Should().Be(ConnectSpanSegmentsAtRouteNodeErrorCodes.OUTER_AND_INNER_SPANS_CANNOT_BE_CONNECTED);
        }

        [Fact, Order(14)]
        public async Task TestConnect5x10ToCustomerConduitAtCC_1_ShouldSucceed()
        {
            MakeSureTestConduitIsCutAtCC_1();

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutConnectFromSpanEquipment = TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10;
            var sutConnectToSpanEquipment = TestUtilityNetwork.CustomerConduit_CC_1_to_SDU_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectFromSpanEquipment, out var sutFromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectToSpanEquipment, out var sutToSpanEquipment);

            // Connect inner conduit 3 in 10x10 to customer conduit
            var connectCmd = new ConnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CC_1,
                spanSegmentsToConnect: new Guid[] {
                    sutFromSpanEquipment.SpanStructures[4].SpanSegments[1].Id,
                    sutToSpanEquipment.SpanStructures[0].SpanSegments[0].Id
                }
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd);

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
            fromEquipmentAfterConnect.SpanStructures[4].SpanSegments[1].FromTerminalId.Should().NotBeEmpty();

            var terminalId = fromEquipmentAfterConnect.SpanStructures[4].SpanSegments[1].FromTerminalId;

            var toEquipmentAfterConnect = toEquipmentQueryResult.Value.SpanEquipment[sutConnectToSpanEquipment];
            toEquipmentAfterConnect.SpanStructures[0].SpanSegments[0].FromTerminalId.Should().Be(terminalId);
        }

        [Fact, Order(15)]
        public async Task TestDisconnect5x10FromCustomerConduitAtCC_1_ShouldSucceed()
        {
            MakeSureTestConduitIsCutAtCC_1();

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutConnectFromSpanEquipment = TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10;
            var sutConnectToSpanEquipment = TestUtilityNetwork.CustomerConduit_CC_1_to_SDU_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectFromSpanEquipment, out var sutFromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectToSpanEquipment, out var sutToSpanEquipment);

            // Disconnect inner conduit 3 in 10x10 to customer conduit
            var connectCmd = new DisconnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CC_1,
                spanSegmentsToDisconnect: new Guid[] {
                    sutFromSpanEquipment.SpanStructures[4].SpanSegments[1].Id,
                    sutToSpanEquipment.SpanStructures[0].SpanSegments[0].Id
                }
            );

            var disconnectResult = await _commandDispatcher.HandleAsync<DisconnectSpanSegmentsAtRouteNode, Result>(connectCmd);

            // Assert
            disconnectResult.IsSuccess.Should().BeTrue();
        }

        [Fact, Order(16)]
        public async Task TestConnect5x10OuterConduitToCustomerAtCC_1_ShouldFail()
        {
            MakeSureTestConduitIsCutAtCC_1();

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutConnectFromSpanEquipment = TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10;
            var sutConnectToSpanEquipment = TestUtilityNetwork.CustomerConduit_CC_1_to_SDU_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectFromSpanEquipment, out var sutFromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectToSpanEquipment, out var sutToSpanEquipment);

            // Connect inner conduit 3 in 6x10 to customer conduit
            var connectCmd = new ConnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CC_1,
                spanSegmentsToConnect: new Guid[] {
                    sutFromSpanEquipment.SpanStructures[0].SpanSegments[1].Id,
                    sutToSpanEquipment.SpanStructures[0].SpanSegments[0].Id
                }
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd);

            var fromEquipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
               new GetEquipmentDetails(new EquipmentIdList() { sutConnectFromSpanEquipment })
            );

            var toEquipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
              new GetEquipmentDetails(new EquipmentIdList() { sutConnectToSpanEquipment })
            );

            // Assert
            connectResult.IsFailed.Should().BeTrue();
        }





        [Fact, Order(100)]
        public async Task TestDetachConduitFromContainerInCC1_ShouldFalid()
        {
            var testConduitId = TestUtilityNetwork.MultiConduit_3x10_CC_1_to_SP_1;

            var testConduit = _eventStore.Projections.Get<UtilityNetworkProjection>().SpanEquipmentsByEquipmentId[testConduitId];

            var nodeContainerId = testConduit.NodeContainerAffixes.First(n => n.RouteNodeId == TestRouteNetwork.CC_1).NodeContainerId;

            var detachConduitFromNodeContainer = new DetachSpanEquipmentFromNodeContainer(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                testConduit.SpanStructures[1].SpanSegments[0].Id,
                routeNodeId: TestRouteNetwork.CC_1
            );

            // Act
            var detachResult = await _commandDispatcher.HandleAsync<DetachSpanEquipmentFromNodeContainer, Result>(detachConduitFromNodeContainer);

            // Assert
            detachResult.IsFailed.Should().BeTrue();

            ((DetachSpanEquipmentFromNodeContainerError)detachResult.Errors.First()).Code.Should().Be(DetachSpanEquipmentFromNodeContainerErrorCodes.SPAN_SEGMENT_IS_CONNECTED_INSIDE_NODE_CONTAINER);

        }


        private async void MakeSureTestConduitIsCutAtCC_1()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipment = TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipment, out var spanEquipment);

            // Cut segments in structure 1 (the outer conduit and second inner conduit)
            var cutCmd = new CutSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CC_1,
                spanSegmentsToCut: new Guid[] {
                    spanEquipment.SpanStructures[0].SpanSegments[0].Id,
                    spanEquipment.SpanStructures[1].SpanSegments[0].Id,
                    spanEquipment.SpanStructures[2].SpanSegments[0].Id,
                    spanEquipment.SpanStructures[3].SpanSegments[0].Id,
                    spanEquipment.SpanStructures[4].SpanSegments[0].Id
                }
            );

            await _commandDispatcher.HandleAsync<CutSpanSegmentsAtRouteNode, Result>(cutCmd);
        }
    }
}

#nullable enable
