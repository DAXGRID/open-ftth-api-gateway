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
    [Order(1300)]
    public class T1300_SpanEquipmentMoveTests
    {
        private readonly IEventStore _eventStore;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly FakeExternalEventProducer _externalEventProducer;

        public T1300_SpanEquipmentMoveTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = (FakeExternalEventProducer)externalEventProducer;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public async Task TestMoveNonConnectedSpanEquipmentEnd_ShouldSucceed()
        {
            MakeSureTestConduitsHasInnerConduitsAndConnections();

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipmentId = TestUtilityNetwork.MultiConduit_6x10_SDU_1_to_J_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipment);

            var moveCmd = new MoveSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipment.Id, new RouteNetworkElementIdList() { TestRouteNetwork.S8 });

            var moveCmdResult = await _commandDispatcher.HandleAsync<MoveSpanEquipment, Result>(moveCmd);

            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
              new GetEquipmentDetails(new EquipmentIdList() { sutSpanEquipmentId })
            );

            var sdu1RouteNetworkQueryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
                new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.SDU_1, TestRouteNetwork.S7 })
                { 
                    RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementOnly 
                }
            );

            var sdu2RouteNetworkQueryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
                new GetRouteNetworkDetails(new RouteNetworkElementIdList() { TestRouteNetwork.SDU_2, TestRouteNetwork.S8 })
                {
                    RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementOnly
                }
            );

            var spanEquipmentAfterMove = equipmentQueryResult.Value.SpanEquipment[sutSpanEquipmentId];

            // Assert
            moveCmdResult.IsSuccess.Should().BeTrue();

            // Check that sut conduit is no longer to be found in SDU_1 and S7
            sdu1RouteNetworkQueryResult.Value.RouteNetworkElements.Any(r => r.InterestRelations.Any(i => i.RefId == spanEquipment.WalkOfInterestId)).Should().BeFalse();

            // Check that sut conduit is now found in SDU_2 and S8
            sdu2RouteNetworkQueryResult.Value.RouteNetworkElements.Count(r => r.InterestRelations.Any(i => i.RefId == spanEquipment.WalkOfInterestId)).Should().Be(2);

            // Check if nodes of interest has changed to reflect new location
            spanEquipmentAfterMove.NodesOfInterestIds.First().Should().Be(TestRouteNetwork.J_1);
            spanEquipmentAfterMove.NodesOfInterestIds.Last().Should().Be(TestRouteNetwork.SDU_2);

            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification.Moved" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == sutSpanEquipmentId)));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.J_1);
        }

        [Fact, Order(10)]
        public async Task TestMoveFromEndToNodeWithCuts_ShouldFail()
        {
            MakeSureTestConduitsHasInnerConduitsAndConnections();

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipmentId = TestUtilityNetwork.FlexConduit_40_Red_SDU_1_to_SDU_2;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipment);

            var moveCmd = new MoveSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipment.Id, new RouteNetworkElementIdList() { TestRouteNetwork.S8 });

            var moveCmdResult = await _commandDispatcher.HandleAsync<MoveSpanEquipment, Result>(moveCmd);
          
            // Assert
            moveCmdResult.IsFailed.Should().BeTrue();
            ((MoveSpanEquipmentError)moveCmdResult.Errors.First()).Code.Should().Be(MoveSpanEquipmentErrorCodes.CANNOT_MOVE_FROM_END_TO_NODE_WHERE_SEGMENTS_ARE_CUT);
        }

        [Fact, Order(11)]
        public async Task TestMoveToEndToNodeWithCuts_ShouldFail()
        {
            MakeSureTestConduitsHasInnerConduitsAndConnections();

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipmentId = TestUtilityNetwork.FlexConduit_40_Red_SDU_1_to_SDU_2;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipment);

            var moveCmd = new MoveSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipment.Id, new RouteNetworkElementIdList() { TestRouteNetwork.S7});

            var moveCmdResult = await _commandDispatcher.HandleAsync<MoveSpanEquipment, Result>(moveCmd);

            // Assert
            moveCmdResult.IsFailed.Should().BeTrue();
            ((MoveSpanEquipmentError)moveCmdResult.Errors.First()).Code.Should().Be(MoveSpanEquipmentErrorCodes.CANNOT_MOVE_TO_END_TO_NODE_WHERE_SEGMENTS_ARE_CUT);
        }

        [Fact, Order(12)]
        public async Task TestMoveBothEndOfSpanEquipmentWithCuts_ShouldFail()
        {
            MakeSureTestConduitsHasInnerConduitsAndConnections();

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipmentId = TestUtilityNetwork.FlexConduit_40_Red_SDU_1_to_SDU_2;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipment);

            var moveCmd = new MoveSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipment.Id, new RouteNetworkElementIdList() { TestRouteNetwork.S11 });

            var moveCmdResult = await _commandDispatcher.HandleAsync<MoveSpanEquipment, Result>(moveCmd);

            // Assert
            moveCmdResult.IsFailed.Should().BeTrue();
            ((MoveSpanEquipmentError)moveCmdResult.Errors.First()).Code.Should().Be(MoveSpanEquipmentErrorCodes.CANNOT_MOVE_BOTH_ENDS_AT_THE_SAME_TIME_IF_SPAN_SEGMENT_HAS_CUTS);
        }

        [Fact, Order(13)]
        public async Task TestMoveEndAffixedToContainer_ShouldFail()
        {
            MakeSureTestConduitsHasInnerConduitsAndConnections();

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipmentId = TestUtilityNetwork.MultiConduit_5x10_SDU_2_to_J_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipment);

            var moveCmd = new MoveSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipment.Id, new RouteNetworkElementIdList() { TestRouteNetwork.S5 });

            var moveCmdResult = await _commandDispatcher.HandleAsync<MoveSpanEquipment, Result>(moveCmd);

            // Assert
            moveCmdResult.IsFailed.Should().BeTrue();
            ((MoveSpanEquipmentError)moveCmdResult.Errors.First()).Code.Should().Be(MoveSpanEquipmentErrorCodes.CANNOT_MOVE_NODE_BECAUSE_SPAN_EQUIPMENT_IS_AFFIXED_TO_CONTAINER);
        }

        [Fact, Order(14)]
        public async Task TestMoveSpanEquipmentToSameWalk_ShouldFail()
        {
            MakeSureTestConduitsHasInnerConduitsAndConnections();

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipmentId = TestUtilityNetwork.FlexConduit_40_Red_SDU_1_to_SDU_2;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipment);

            var moveCmd = new MoveSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipment.Id, new RouteNetworkElementIdList() { TestRouteNetwork.S8, TestRouteNetwork.S7 });

            var moveCmdResult = await _commandDispatcher.HandleAsync<MoveSpanEquipment, Result>(moveCmd);

            // Assert
            moveCmdResult.IsFailed.Should().BeTrue();
            ((MoveSpanEquipmentError)moveCmdResult.Errors.First()).Code.Should().Be(MoveSpanEquipmentErrorCodes.NEW_WALK_EQUALS_EXISTING_WALK);
        }

        [Fact, Order(15)]
        public async Task TestMoveSpanEndWithConnections_ShouldFail()
        {
            MakeSureTestConduitsHasInnerConduitsAndConnections();
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            // Connect flex inner conduit 2 with 5x10 inner conduit 1
            utilityNetwork.TryGetEquipment<SpanEquipment>(TestUtilityNetwork.FlexConduit_40_Red_SDU_1_to_SDU_2, out var sutFromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(TestUtilityNetwork.MultiConduit_5x10_SDU_2_to_J_1, out var sutToSpanEquipment);

            var connectCmd = new ConnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.J_1,
                spanSegmentsToConnect: new Guid[] {
                    sutFromSpanEquipment.SpanStructures[3].SpanSegments[0].Id,
                    sutToSpanEquipment.SpanStructures[1].SpanSegments[0].Id
                }
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd);
            connectResult.IsSuccess.Should().BeTrue();

            // Act
            var sutSpanEquipmentId = TestUtilityNetwork.MultiConduit_5x10_SDU_2_to_J_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipment);

            var moveCmd = new MoveSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipment.Id, new RouteNetworkElementIdList() { TestRouteNetwork.S7, TestRouteNetwork.S8 });

            var moveCmdResult = await _commandDispatcher.HandleAsync<MoveSpanEquipment, Result>(moveCmd);

            // Assert
            moveCmdResult.IsFailed.Should().BeTrue();
            ((MoveSpanEquipmentError)moveCmdResult.Errors.First()).Code.Should().Be(MoveSpanEquipmentErrorCodes.CANNOT_MOVE_FROM_END_BECAUSE_SEGMENTS_ARE_CONNECTED_THERE);
        }


        private async void MakeSureTestConduitsHasInnerConduitsAndConnections()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipmentId = TestUtilityNetwork.FlexConduit_40_Red_SDU_1_to_SDU_2;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipment);

            if (spanEquipment.SpanStructures.Length > 1)
                return;

            // Add 3 inner conduits
            var addStructure = new PlaceAdditionalStructuresInSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty),
              spanEquipmentId: sutSpanEquipmentId,
              structureSpecificationIds: new Guid[] { TestSpecifications.Ø10_Red, TestSpecifications.Ø10_Black, TestSpecifications.Ø10_Violet }
            );

            var addStructureResult = await _commandDispatcher.HandleAsync<PlaceAdditionalStructuresInSpanEquipment, Result>(addStructure);

            addStructureResult.IsSuccess.Should().BeTrue();

            // Cut flex conduit inner conduit 1 & 2
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipment2);

            var cutCmd = new CutSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.J_1,
                spanSegmentsToCut: new Guid[] {
                    spanEquipment2.SpanStructures[0].SpanSegments[0].Id,
                    spanEquipment2.SpanStructures[1].SpanSegments[0].Id,
                    spanEquipment2.SpanStructures[2].SpanSegments[0].Id,
                    spanEquipment2.SpanStructures[3].SpanSegments[0].Id
                }
            );

            var cutCmdResult = await _commandDispatcher.HandleAsync<CutSpanSegmentsAtRouteNode, Result>(cutCmd);
            cutCmdResult.IsSuccess.Should().BeTrue();

            // Cut 3x10 conduit 1
            utilityNetwork.TryGetEquipment<SpanEquipment>(TestUtilityNetwork.MultiConduit_3x10_CC_1_to_HH_11, out var spanEquipment3);

            var cutCmd2 = new CutSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.J_1,
                spanSegmentsToCut: new Guid[] {
                    spanEquipment3.SpanStructures[0].SpanSegments[0].Id,
                    spanEquipment3.SpanStructures[1].SpanSegments[0].Id
                }
            );

            // Might already be cut in SpanEquipmentCutTests
            await _commandDispatcher.HandleAsync<CutSpanSegmentsAtRouteNode, Result>(cutCmd2);


            // Connect flex inner conduit 1 with 3-10 inner conduit 1
            utilityNetwork.TryGetEquipment<SpanEquipment>(TestUtilityNetwork.FlexConduit_40_Red_SDU_1_to_SDU_2, out var sutFromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(TestUtilityNetwork.MultiConduit_3x10_CC_1_to_HH_11, out var sutToSpanEquipment);

            var connectCmd = new ConnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.J_1,
                spanSegmentsToConnect: new Guid[] {
                    sutFromSpanEquipment.SpanStructures[1].SpanSegments[0].Id,
                    sutToSpanEquipment.SpanStructures[1].SpanSegments[0].Id
                }
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd);
            connectResult.IsSuccess.Should().BeTrue();

        }
    }
}

#nullable enable
