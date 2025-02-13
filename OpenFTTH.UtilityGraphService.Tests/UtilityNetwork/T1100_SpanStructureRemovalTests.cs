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
    [Order(1100)]
    public class T1100_SpanStructureRemovalTests
    {
        private readonly IEventStore _eventStore;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly FakeExternalEventProducer _externalEventProducer;

        public T1100_SpanStructureRemovalTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = (FakeExternalEventProducer)externalEventProducer;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public async Task TestRemoveNonConnectedStructureFromNonFixedSpanEquipment_ShouldSucceed()
        {
            MakeSureTestConduitsHasInnerConduitsAndConnections();

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipmentId = TestUtilityNetwork.FlexConduit_40_Red_SDU_1_to_SDU_2;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var spanEquipment);

            var removeStructureCmd = new RemoveSpanStructureFromSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipment.SpanStructures[2].SpanSegments[0].Id);

            var removeStructureCmdResult = await _commandDispatcher.HandleAsync<RemoveSpanStructureFromSpanEquipment, Result>(removeStructureCmd);

            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
              new GetEquipmentDetails(new EquipmentIdList() { sutSpanEquipmentId })
            );

            var equipmentAfterRemovingStructure = equipmentQueryResult.Value.SpanEquipment[sutSpanEquipmentId];

            // Assert
            removeStructureCmdResult.IsSuccess.Should().BeTrue();
            equipmentAfterRemovingStructure.SpanStructures[2].Deleted.Should().BeTrue();

            // Make sure segment is removed from the graph as well
            utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphElement>(equipmentAfterRemovingStructure.SpanStructures[2].SpanSegments[0].Id, out _).Should().BeFalse();

            // Check if an event is published to the notification.utility-network topic having an idlist containing the span equipment id we just created
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification.StructuresRemoved" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == sutSpanEquipmentId)));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(TestRouteNetwork.J_1);

        }

        [Fact, Order(10)]
        public async Task TestRemoveInnerStructureFromFixedSpanEquipment_ShouldFail()
        {
            MakeSureTestConduitsHasInnerConduitsAndConnections();

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipment = TestUtilityNetwork.MultiConduit_3x10_CC_1_to_SP_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipment, out var spanEquipment);

            var removeStructureCmd = new RemoveSpanStructureFromSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipment.SpanStructures[2].SpanSegments[0].Id);

            var removeStructureCmdResult = await _commandDispatcher.HandleAsync<RemoveSpanStructureFromSpanEquipment, Result>(removeStructureCmd);

            // Assert
            removeStructureCmdResult.IsFailed.Should().BeTrue();
            ((RemoveSpanStructureFromSpanEquipmentError)removeStructureCmdResult.Errors.First()).Code.Should().Be(RemoveSpanStructureFromSpanEquipmentErrorCodes.CANNOT_REMOVE_SPAN_STRUCTURES_FROM_FIXED_SPAN_EQUIPMENT);

        }

        [Fact, Order(11)]
        public async Task TestRemoveConnectedInnerStructureFromSpanEquipment_ShouldFail()
        {
            MakeSureTestConduitsHasInnerConduitsAndConnections();

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipment = TestUtilityNetwork.FlexConduit_40_Red_SDU_1_to_SDU_2;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipment, out var spanEquipment);

            var removeStructureCmd = new RemoveSpanStructureFromSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipment.SpanStructures[1].SpanSegments[0].Id);

            var removeStructureCmdResult = await _commandDispatcher.HandleAsync<RemoveSpanStructureFromSpanEquipment, Result>(removeStructureCmd);

            // Assert
            removeStructureCmdResult.IsFailed.Should().BeTrue();
            ((RemoveSpanStructureFromSpanEquipmentError)removeStructureCmdResult.Errors.First()).Code.Should().Be(RemoveSpanStructureFromSpanEquipmentErrorCodes.CANNOT_REMOVE_SPAN_STRUCTURE_WITH_CONNECTED_SEGMENTS_FROM_SPAN_EQUIPMENT);

        }

        [Fact, Order(12)]
        public async Task TestRemoveOuterStructureFromSpanEquipmentWithConnectedInnerStructure_ShouldFail()
        {
            MakeSureTestConduitsHasInnerConduitsAndConnections();

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipment = TestUtilityNetwork.FlexConduit_40_Red_SDU_1_to_SDU_2;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipment, out var spanEquipment);

            var removeStructureCmd = new RemoveSpanStructureFromSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipment.SpanStructures[0].SpanSegments[0].Id);

            var removeStructureCmdResult = await _commandDispatcher.HandleAsync<RemoveSpanStructureFromSpanEquipment, Result>(removeStructureCmd);

            // Assert
            removeStructureCmdResult.IsFailed.Should().BeTrue();
            //((RemoveSpanStructureFromSpanEquipmentError)removeStructureCmdResult.Errors.First()).Code.Should().Be(RemoveSpanStructureFromSpanEquipmentErrorCodes.CANNOT_REMOVE_SPAN_STRUCTURE_WITH_CONNECTED_SEGMENTS_FROM_SPAN_EQUIPMENT);
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

            // Cut flex conduit inner conduit 1
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
