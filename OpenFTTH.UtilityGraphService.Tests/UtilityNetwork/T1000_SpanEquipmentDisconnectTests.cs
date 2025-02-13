using DAX.EventProcessing;
using FluentAssertions;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.UtilityNetwork;
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
    [Order(1000)]
    public class T1000_SpanEquipmentDisconnectTests
    {
        private readonly IEventStore _eventStore;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly FakeExternalEventProducer _externalEventProducer;

        public T1000_SpanEquipmentDisconnectTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = (FakeExternalEventProducer)externalEventProducer;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public async Task TestDisconnect5x10From3x10ConduitAtCC_1_ShouldSucceed()
        {
           MakeSureTestConduitIsCutAndConnectedAtCC_1();

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutConnectFromSpanEquipment = TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10;
            var sutConnectToSpanEquipment = TestUtilityNetwork.MultiConduit_3x10_CC_1_to_SP_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectFromSpanEquipment, out var sutFromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectToSpanEquipment, out var sutToSpanEquipment);

            // Disconnect inner conduit 4 in 6x10 from inner conduit 3 in 3x10
            var disconnectCmd = new DisconnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CC_1,
                spanSegmentsToDisconnect: new Guid[] {
                    sutFromSpanEquipment.SpanStructures[4].SpanSegments[0].Id,
                    sutToSpanEquipment.SpanStructures[3].SpanSegments[0].Id
                }
            );

            var disconnectResult = await _commandDispatcher.HandleAsync<DisconnectSpanSegmentsAtRouteNode, Result>(disconnectCmd);

            var fromEquipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
               new GetEquipmentDetails(new EquipmentIdList() { sutConnectFromSpanEquipment })
            );

            var toEquipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
              new GetEquipmentDetails(new EquipmentIdList() { sutConnectToSpanEquipment })
            );

            // Assert
            disconnectResult.IsSuccess.Should().BeTrue();
            fromEquipmentQueryResult.IsSuccess.Should().BeTrue();
            toEquipmentQueryResult.IsSuccess.Should().BeTrue();

            var fromEquipmentAfterDisconnect = fromEquipmentQueryResult.Value.SpanEquipment[sutConnectFromSpanEquipment];
            fromEquipmentAfterDisconnect.SpanStructures[4].SpanSegments[0].ToTerminalId.Should().BeEmpty();
            fromEquipmentAfterDisconnect.SpanStructures[4].SpanSegments[0].FromTerminalId.Should().BeEmpty();

            var toEquipmentAfterDisconnect = toEquipmentQueryResult.Value.SpanEquipment[sutConnectToSpanEquipment];
            toEquipmentAfterDisconnect.SpanStructures[3].SpanSegments[0].FromTerminalId.Should().BeEmpty();
            toEquipmentAfterDisconnect.SpanStructures[3].SpanSegments[0].ToTerminalId.Should().BeEmpty();

            // Check utility graph
            utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(fromEquipmentAfterDisconnect.SpanStructures[4].SpanSegments[0].Id, out var fromGraphSegmentRef);
            fromGraphSegmentRef.SpanSegment(utilityNetwork).FromTerminalId.Should().BeEmpty();
            fromGraphSegmentRef.SpanSegment(utilityNetwork).ToTerminalId.Should().BeEmpty();

            utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(toEquipmentAfterDisconnect.SpanStructures[3].SpanSegments[0].Id, out var toGraphSegmentRef);
            toGraphSegmentRef.SpanSegment(utilityNetwork).FromTerminalId.Should().BeEmpty();
            toGraphSegmentRef.SpanSegment(utilityNetwork).ToTerminalId.Should().BeEmpty();

            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentConnectivityModification.Disconnect" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == sutConnectFromSpanEquipment)));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.CC_1);
        }

        [Fact, Order(2)]
        public async Task TestDisconnectSameSegmentsInSameSpanEquipment_ShouldFail()
        {
            MakeSureTestConduitIsCutAndConnectedAtCC_1();

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutConnectFromSpanEquipment = TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10;
            var sutConnectToSpanEquipment = TestUtilityNetwork.MultiConduit_3x10_CC_1_to_SP_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectFromSpanEquipment, out var sutFromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectToSpanEquipment, out var sutToSpanEquipment);

            // Disconnect the same inner conduit from itself
            var disconnectCmd = new DisconnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CC_1,
                spanSegmentsToDisconnect: new Guid[] {
                    sutFromSpanEquipment.SpanStructures[1].SpanSegments[0].Id,
                    sutFromSpanEquipment.SpanStructures[1].SpanSegments[0].Id,
                }
            );

            var disconnectResult = await _commandDispatcher.HandleAsync<DisconnectSpanSegmentsAtRouteNode, Result>(disconnectCmd);

            var fromEquipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
               new GetEquipmentDetails(new EquipmentIdList() { sutConnectFromSpanEquipment })
            );

            var toEquipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
              new GetEquipmentDetails(new EquipmentIdList() { sutConnectToSpanEquipment })
            );

            // Assert
            disconnectResult.IsFailed.Should().BeTrue();
            ((DisconnectSpanSegmentsAtRouteNodeError)disconnectResult.Errors.First()).Code.Should().Be(DisconnectSpanSegmentsAtRouteNodeErrorCodes.CANNOT_DISCONNECT_SPAN_SEGMENT_TO_ITSELF);
        }

        [Fact, Order(3)]
        public async Task TestDisconnectDifferentSegmentsInSameSpanEquipment_ShouldFail()
        {
            MakeSureTestConduitIsCutAndConnectedAtCC_1();

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutConnectFromSpanEquipment = TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10;
            var sutConnectToSpanEquipment = TestUtilityNetwork.MultiConduit_3x10_CC_1_to_SP_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectFromSpanEquipment, out var sutFromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectToSpanEquipment, out var sutToSpanEquipment);

            // Disconnect the same inner conduit from itself
            var disconnectCmd = new DisconnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CC_1,
                spanSegmentsToDisconnect: new Guid[] {
                    sutFromSpanEquipment.SpanStructures[1].SpanSegments[0].Id,
                    sutFromSpanEquipment.SpanStructures[2].SpanSegments[0].Id,
                }
            );

            var disconnectResult = await _commandDispatcher.HandleAsync<DisconnectSpanSegmentsAtRouteNode, Result>(disconnectCmd);

            var fromEquipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
               new GetEquipmentDetails(new EquipmentIdList() { sutConnectFromSpanEquipment })
            );

            var toEquipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
              new GetEquipmentDetails(new EquipmentIdList() { sutConnectToSpanEquipment })
            );

            // Assert
            disconnectResult.IsFailed.Should().BeTrue();
            ((DisconnectSpanSegmentsAtRouteNodeError)disconnectResult.Errors.First()).Code.Should().Be(DisconnectSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENTS_ARE_NOT_CONNECTED);
        }


        private async void MakeSureTestConduitIsCutAndConnectedAtCC_1()
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


            var sutConnectFromSpanEquipment = TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10;
            var sutConnectToSpanEquipment = TestUtilityNetwork.MultiConduit_3x10_CC_1_to_SP_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectFromSpanEquipment, out var sutFromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConnectToSpanEquipment, out var sutToSpanEquipment);

            // Connect inner conduit 2 in 5x10 with inner conduit 3 in 3x10
            var connectCmd = new ConnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CC_1,
                spanSegmentsToConnect: new Guid[] {
                    sutFromSpanEquipment.SpanStructures[4].SpanSegments[0].Id,
                    sutToSpanEquipment.SpanStructures[3].SpanSegments[0].Id
                }
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd);
        }
    }
}

#nullable enable
