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
    [Order(1400)]
    public class T1400_SpanEquipmentMergeTests
    {
        private readonly IEventStore _eventStore;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly FakeExternalEventProducer _externalEventProducer;

        public T1400_SpanEquipmentMergeTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = (FakeExternalEventProducer)externalEventProducer;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public async Task ConnectConduitInSDU1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipment1 = TestUtilityNetwork.MultiConduit_3x10_SDU_1_to_SDU_2;
            var sutSpanEquipment2 = TestUtilityNetwork.MultiConduit_12x7_SDU_1_to_J_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipment1, out var spanEquipment1BeforeConnect);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipment2, out var spanEquipment2BeforeConnect);

            // Connect inner conduit 1
            var connectCmd = new ConnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.SDU_1,
                spanSegmentsToConnect: new Guid[] {
                    spanEquipment1BeforeConnect.SpanStructures[1].SpanSegments[0].Id,
                    spanEquipment2BeforeConnect.SpanStructures[1].SpanSegments[0].Id
                }
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd);

            // Assert
            connectResult.IsSuccess.Should().BeTrue();

            // Assert trace is correct
            var traceQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                   new GetEquipmentDetails(new EquipmentIdList() { sutSpanEquipment2 })
                   {
                       EquipmentDetailsFilter = new EquipmentDetailsFilterOptions()
                       {
                           IncludeRouteNetworkTrace = true
                       }
                   }
               );

            traceQueryResult.IsSuccess.Should().BeTrue();

            var innerConduit1TraceId = traceQueryResult.Value.SpanEquipment[sutSpanEquipment2].RouteNetworkTraceRefs.First(tr => tr.SpanEquipmentOrSegmentId == spanEquipment2BeforeConnect.SpanStructures[1].SpanSegments[0].Id).TraceId;
            var routeNetworkTraces = traceQueryResult.Value.RouteNetworkTraces[innerConduit1TraceId];

            routeNetworkTraces.FromRouteNodeId.Should().Be(TestRouteNetwork.J_1);
            routeNetworkTraces.ToRouteNodeId.Should().Be(TestRouteNetwork.SDU_2);
        }

        [Fact, Order(10)]
        public async Task MergeTheTwo12x7inJ_1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            // NB: Both span equipment have from node in J_1
            var sutFromSpanEquipmentId = TestUtilityNetwork.MultiConduit_12x7_SDU_1_to_J_1;
            var sutToSpanEquipmentId = TestUtilityNetwork.MultiConduit_12x7_SDU_2_to_J_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutFromSpanEquipmentId, out var fromSpanEquipmentBeforeConnect);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutToSpanEquipmentId, out var toSpanEquipmentBeforeConnect);

            // Connect the two outer conduits, which should result in a merge of the two span equipments
            var connectCmd = new ConnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.J_1,
                spanSegmentsToConnect: new Guid[] {
                    fromSpanEquipmentBeforeConnect.SpanStructures[0].SpanSegments[0].Id,
                    toSpanEquipmentBeforeConnect.SpanStructures[0].SpanSegments[0].Id
                }
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd);

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutFromSpanEquipmentId, out var fromSpanEquipmentAfterConnect);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutToSpanEquipmentId, out var toSpanEquipmentAfterConnect);

            var j1NetworkQueryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
               new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.J_1 })
               {
                   RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
               }
            );


            // Assert
            connectResult.IsSuccess.Should().BeTrue();
            j1NetworkQueryResult.IsSuccess.Should().BeTrue();

            // from span equipment must still be referenced from J_1
            j1NetworkQueryResult.Value.RouteNetworkElements[TestRouteNetwork.J_1].InterestRelations.Any(i => i.RefId == fromSpanEquipmentBeforeConnect.WalkOfInterestId).Should().BeTrue();

            // to span equipment must no longer be referenced from J_1
            j1NetworkQueryResult.Value.RouteNetworkElements[TestRouteNetwork.J_1].InterestRelations.Any(i => i.RefId == toSpanEquipmentBeforeConnect.WalkOfInterestId).Should().BeFalse();

            // to span equipment must no have an interest
            j1NetworkQueryResult.Value.Interests.ContainsKey(toSpanEquipmentBeforeConnect.WalkOfInterestId).Should().BeFalse();

            // Check interest information of from span equipment
            j1NetworkQueryResult.Value.Interests[fromSpanEquipmentBeforeConnect.WalkOfInterestId].RouteNetworkElementRefs.Count().Should().Be(5);
            j1NetworkQueryResult.Value.Interests[fromSpanEquipmentBeforeConnect.WalkOfInterestId].RouteNetworkElementRefs[0].Should().Be(TestRouteNetwork.SDU_2);
            j1NetworkQueryResult.Value.Interests[fromSpanEquipmentBeforeConnect.WalkOfInterestId].RouteNetworkElementRefs[1].Should().Be(TestRouteNetwork.S8);
            j1NetworkQueryResult.Value.Interests[fromSpanEquipmentBeforeConnect.WalkOfInterestId].RouteNetworkElementRefs[2].Should().Be(TestRouteNetwork.J_1);
            j1NetworkQueryResult.Value.Interests[fromSpanEquipmentBeforeConnect.WalkOfInterestId].RouteNetworkElementRefs[3].Should().Be(TestRouteNetwork.S7);
            j1NetworkQueryResult.Value.Interests[fromSpanEquipmentBeforeConnect.WalkOfInterestId].RouteNetworkElementRefs[4].Should().Be(TestRouteNetwork.SDU_1);

            fromSpanEquipmentAfterConnect.Should().NotBeNull();

            // To span equipment should be deleted
            toSpanEquipmentAfterConnect.Should().BeNull();

            // From span segment should still end in SDU_1
            fromSpanEquipmentAfterConnect.NodesOfInterestIds.Last().Should().Be(TestRouteNetwork.SDU_1);

            // From span segment should now start en SDU 2
            fromSpanEquipmentAfterConnect.NodesOfInterestIds.First().Should().Be(TestRouteNetwork.SDU_2);

            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification.Merge" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == sutFromSpanEquipmentId)));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.J_1);
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.SDU_1);
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.SDU_2);

            // Assert trace is correct
            var traceQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                   new GetEquipmentDetails(new EquipmentIdList() { sutFromSpanEquipmentId })
                   {
                       EquipmentDetailsFilter = new EquipmentDetailsFilterOptions()
                       {
                           IncludeRouteNetworkTrace = true
                       }
                   }
               );

            traceQueryResult.IsSuccess.Should().BeTrue();

            var innerConduit1TraceId = traceQueryResult.Value.SpanEquipment[sutFromSpanEquipmentId].RouteNetworkTraceRefs.First(tr => tr.SpanEquipmentOrSegmentId == fromSpanEquipmentBeforeConnect.SpanStructures[1].SpanSegments[0].Id).TraceId;

            var routeNetworkTraces = traceQueryResult.Value.RouteNetworkTraces[innerConduit1TraceId];
            routeNetworkTraces.FromRouteNodeId.Should().Be(TestRouteNetwork.SDU_2);
            routeNetworkTraces.ToRouteNodeId.Should().Be(TestRouteNetwork.SDU_2);
        }

        [Fact, Order(11)]
        public async Task TestAffixConduitToContainer_ShouldSucceed()
        {
            var testConduitId = TestUtilityNetwork.MultiConduit_12x7_SDU_1_to_J_1;

            var testConduit = _eventStore.Projections.Get<UtilityNetworkProjection>().SpanEquipmentsByEquipmentId[testConduitId];

            var affixConduitToContainerCommand = new AffixSpanEquipmentToNodeContainer(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                spanEquipmentOrSegmentId: testConduit.SpanStructures[0].SpanSegments[0].Id,
                nodeContainerId: TestUtilityNetwork.NodeContainer_J_1,
                nodeContainerIngoingSide: NodeContainerSideEnum.West
            );

            var affixResult = await _commandDispatcher.HandleAsync<AffixSpanEquipmentToNodeContainer, Result>(affixConduitToContainerCommand);

            affixResult.IsSuccess.Should().BeTrue();
        }



        [Fact, Order(12)]
        public async Task CutMergeConduitInJ_1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipment = TestUtilityNetwork.MultiConduit_12x7_SDU_1_to_J_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipment, out var spanEquipment);

            // Cut the outer conduit and 4 inner conduit
            var cutCmd = new CutSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.J_1,
                spanSegmentsToCut: new Guid[] {
                    spanEquipment.SpanStructures[0].SpanSegments[0].Id,
                    spanEquipment.SpanStructures[1].SpanSegments[0].Id,
                    spanEquipment.SpanStructures[2].SpanSegments[0].Id,
                    spanEquipment.SpanStructures[3].SpanSegments[0].Id,
                    spanEquipment.SpanStructures[4].SpanSegments[0].Id
                }
            );

            var cutResult = await _commandDispatcher.HandleAsync<CutSpanSegmentsAtRouteNode, Result>(cutCmd);

            cutResult.IsSuccess.Should().BeTrue();
        }


        [Fact, Order(13)]
        public async Task ConnectInMergedConduitInJ_1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipment = TestUtilityNetwork.MultiConduit_12x7_SDU_1_to_J_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipment, out var spanEquipmentBeforeCut);

            // Connect inner conduit 1
            var connectCmd = new ConnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.J_1,
                spanSegmentsToConnect: new Guid[] {
                    spanEquipmentBeforeCut.SpanStructures[1].SpanSegments[0].Id,
                    spanEquipmentBeforeCut.SpanStructures[1].SpanSegments[1].Id
                }
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd);

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipment, out var spanEquipmentAfterConnect);

            // Assert
            connectResult.IsSuccess.Should().BeTrue();
        }

        [Fact, Order(14)]
        public async Task DisconnectInMergedConduitInJ_1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipment = TestUtilityNetwork.MultiConduit_12x7_SDU_1_to_J_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipment, out var spanEquipmentBeforeCut);

            // Connect inner conduit 1
            var disconnectCmd = new DisconnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.J_1,
                spanSegmentsToDisconnect: new Guid[] {
                    spanEquipmentBeforeCut.SpanStructures[1].SpanSegments[0].Id,
                    spanEquipmentBeforeCut.SpanStructures[1].SpanSegments[1].Id
                }
            );

            var disconnectResult = await _commandDispatcher.HandleAsync<DisconnectSpanSegmentsAtRouteNode, Result>(disconnectCmd);

            // Assert
            disconnectResult.IsSuccess.Should().BeTrue();
        }

        [Fact, Order(15)]
        public async Task ConnectAgainInMergedConduitInJ_1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipment = TestUtilityNetwork.MultiConduit_12x7_SDU_1_to_J_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipment, out var spanEquipmentBeforeCut);

            // Connect inner conduit 1
            var connectCmd = new ConnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.J_1,
                spanSegmentsToConnect: new Guid[] {
                    spanEquipmentBeforeCut.SpanStructures[1].SpanSegments[0].Id,
                    spanEquipmentBeforeCut.SpanStructures[1].SpanSegments[1].Id
                }
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd);

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipment, out var spanEquipmentAfterConnect);

            // Assert
            connectResult.IsSuccess.Should().BeTrue();
        }







        [Fact, Order(20)]
        public async Task TryMerge12x7With5x10_ShouldFail()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            // NB: Both span equipment have from node in J_1
            var sutFromSpanEquipmentId = TestUtilityNetwork.MultiConduit_12x7_SDU_1_to_J_1;
            var sutToSpanEquipmentId = TestUtilityNetwork.MultiConduit_6x10_SDU_1_to_J_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutFromSpanEquipmentId, out var fromSpanEquipmentBeforeConnect);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutToSpanEquipmentId, out var toSpanEquipmentBeforeConnect);

            // Connect the two outer conduits, which should result in a merge of the two span equipments
            var connectCmd = new ConnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.J_1,
                spanSegmentsToConnect: new Guid[] {
                    fromSpanEquipmentBeforeConnect.SpanStructures[0].SpanSegments[0].Id,
                    toSpanEquipmentBeforeConnect.SpanStructures[0].SpanSegments[0].Id
                }
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd);

            // Assert
            connectResult.IsFailed.Should().BeTrue();
            //((MergeSpanEquipmentError)connectResult.Errors.First()).Code.Should().Be(MergeSpanEquipmentErrorCodes.CANNOT_MERGE_SPAN_EQUIPMENT_BECAUSE_OF_SPECIFICATION_MISMATCH);
        }

        [Fact, Order(21)]
        public async Task TryMergeNotConnectedSpanEquipments_ShouldFail()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            // NB: Both span equipment have from node in J_1
            var sutFromSpanEquipmentId = TestUtilityNetwork.MultiConduit_6x10_SDU_1_to_J_1;
            var sutToSpanEquipmentId = TestUtilityNetwork.MultiConduit_6x10_HH_1_to_HH_10;
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutFromSpanEquipmentId, out var fromSpanEquipmentBeforeConnect);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutToSpanEquipmentId, out var toSpanEquipmentBeforeConnect);

            // Connect the two outer conduits, which should result in a merge of the two span equipments
            var connectCmd = new ConnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.SDU_1,
                spanSegmentsToConnect: new Guid[] {
                    fromSpanEquipmentBeforeConnect.SpanStructures[0].SpanSegments[0].Id,
                    toSpanEquipmentBeforeConnect.SpanStructures[0].SpanSegments[0].Id
                }
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd);

            // Assert
            connectResult.IsFailed.Should().BeTrue();
            ((MergeSpanEquipmentError)connectResult.Errors.First()).Code.Should().Be(MergeSpanEquipmentErrorCodes.CANNOT_MERGE_SPAN_EQUIPMENT_BECAUSE_ENDS_ARE_NOT_COLOCATED_IN_ROUTE_NODE);
        }
    }
}

#nullable enable
