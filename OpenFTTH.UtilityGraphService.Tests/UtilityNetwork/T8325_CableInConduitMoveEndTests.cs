using FluentAssertions;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.TestData;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.Business.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions.Ordering;

namespace OpenFTTH.UtilityGraphService.Tests.UtilityNetwork
{
    [Order(8325)]
    public class T8325_CableInConduitMoveEndTests
    {
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;
        private ConduitTestUtilityNetwork _conduitTestUtilityNetwork;

        public T8325_CableInConduitMoveEndTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            _conduitTestUtilityNetwork = new ConduitTestUtilityNetwork(_eventStore, _commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public async Task AffixCableInConduit_N1_N2_4_And_ExtentConduitToN3_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutConduitId = ConduitTestUtilityNetwork.Conduit_N1_N2_4;

            var sutCable1 = _conduitTestUtilityNetwork.PlaceCableDirectlyInRouteNetwork("K8325_1", TestSpecifications.FiberCable_12Fiber,
                new Guid[] { ConduitTestUtilityNetwork.S7, ConduitTestUtilityNetwork.S1 });
                  
            // Affix cable 1 into Conduit N1_N2_4
            var cable1AfterFirstAffix = _conduitTestUtilityNetwork.AffixCableToSingleConduit(ConduitTestUtilityNetwork.N2, sutCable1.Id, sutConduitId);


            var sutCable2 = _conduitTestUtilityNetwork.PlaceCableDirectlyInRouteNetwork("K8325_2", TestSpecifications.FiberCable_12Fiber,
                new Guid[] { ConduitTestUtilityNetwork.S1, ConduitTestUtilityNetwork.S7 });

            // Affix cable 2 into Conduit N1_N2_4
            var cable2AfterFirstAffix = _conduitTestUtilityNetwork.AffixCableToSingleConduit(ConduitTestUtilityNetwork.N2, sutCable2.Id, sutConduitId);

            // Extent conduit to N3
            var moveCmd = new MoveSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), sutConduitId, new RouteNetworkElementIdList() { ConduitTestUtilityNetwork.S1, ConduitTestUtilityNetwork.S2 });

            var moveCmdResult = await _commandDispatcher.HandleAsync<MoveSpanEquipment, Result>(moveCmd);

            moveCmdResult.IsSuccess.Should().BeTrue();

            // Check cable 1
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutCable1.Id, out var cable1AfterMove);

            var sutCableWalkOfInterestAfterConduitMove = _conduitTestUtilityNetwork.GetWalkOfInterest(sutCable1.WalkOfInterestId);
            sutCableWalkOfInterestAfterConduitMove.Count().Should().Be(7);
            sutCableWalkOfInterestAfterConduitMove.First().Should().Be(ConduitTestUtilityNetwork.N5);
            sutCableWalkOfInterestAfterConduitMove.Last().Should().Be(ConduitTestUtilityNetwork.N3);

            cable1AfterMove.NodesOfInterestIds.First().Should().Be(sutCableWalkOfInterestAfterConduitMove.First());
            cable1AfterMove.NodesOfInterestIds.Last().Should().Be(sutCableWalkOfInterestAfterConduitMove.Last());

            // Check that hop got new to node id
            cable1AfterMove.UtilityNetworkHops.Last().FromNodeId.Should().Be(ConduitTestUtilityNetwork.N1);
            cable1AfterMove.UtilityNetworkHops.Last().ToNodeId.Should().Be(ConduitTestUtilityNetwork.N3);

            // Check cable 2
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutCable2.Id, out var cable2AfterMove);

            var sutCable2WalkOfInterestAfterConduitMove = _conduitTestUtilityNetwork.GetWalkOfInterest(sutCable2.WalkOfInterestId);
            sutCable2WalkOfInterestAfterConduitMove.Count().Should().Be(7);
            sutCable2WalkOfInterestAfterConduitMove.First().Should().Be(ConduitTestUtilityNetwork.N3);
            sutCable2WalkOfInterestAfterConduitMove.Last().Should().Be(ConduitTestUtilityNetwork.N5);

            cable2AfterMove.NodesOfInterestIds.First().Should().Be(sutCable2WalkOfInterestAfterConduitMove.First());
            cable2AfterMove.NodesOfInterestIds.Last().Should().Be(sutCable2WalkOfInterestAfterConduitMove.Last());

            // Check that hop got new to node id
            cable2AfterMove.UtilityNetworkHops.Last().FromNodeId.Should().Be(ConduitTestUtilityNetwork.N3);
            cable2AfterMove.UtilityNetworkHops.Last().ToNodeId.Should().Be(ConduitTestUtilityNetwork.N1);
        }

        [Fact, Order(2)]
        public async Task ShortenConduit_N1_N2_4_ToInitialExtend_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutConduitId = ConduitTestUtilityNetwork.Conduit_N1_N2_4;
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutConduitId, out var sutConduit);

            // Move conduit to N1_N2 again
            var moveCmd = new MoveSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), sutConduitId, new RouteNetworkElementIdList() { ConduitTestUtilityNetwork.S1 });

            var moveCmdResult = await _commandDispatcher.HandleAsync<MoveSpanEquipment, Result>(moveCmd);

            moveCmdResult.IsSuccess.Should().BeTrue();

            // Check cable 1
            var sutCable1 = utilityNetwork.SpanEquipmentsByEquipmentId.First(s => s.Value.NamingInfo != null && s.Value.NamingInfo.Name == "K8325_1").Value;
   
            var sutCableWalkOfInterestAfterConduitMove = _conduitTestUtilityNetwork.GetWalkOfInterest(sutCable1.WalkOfInterestId);
            sutCableWalkOfInterestAfterConduitMove.Count().Should().Be(5);
            sutCableWalkOfInterestAfterConduitMove.First().Should().Be(ConduitTestUtilityNetwork.N5);
            sutCableWalkOfInterestAfterConduitMove.Last().Should().Be(ConduitTestUtilityNetwork.N2);

            sutCable1.NodesOfInterestIds.First().Should().Be(sutCableWalkOfInterestAfterConduitMove.First());
            sutCable1.NodesOfInterestIds.Last().Should().Be(sutCableWalkOfInterestAfterConduitMove.Last());

            // Check that hop got new to node id
            sutCable1.UtilityNetworkHops.Last().FromNodeId.Should().Be(ConduitTestUtilityNetwork.N1);
            sutCable1.UtilityNetworkHops.Last().ToNodeId.Should().Be(ConduitTestUtilityNetwork.N2);


            // Check cable 2
            var sutCable2 = utilityNetwork.SpanEquipmentsByEquipmentId.First(s => s.Value.NamingInfo != null && s.Value.NamingInfo.Name == "K8325_2").Value;

            var sutCable2WalkOfInterestAfterConduitMove = _conduitTestUtilityNetwork.GetWalkOfInterest(sutCable2.WalkOfInterestId);
            sutCable2WalkOfInterestAfterConduitMove.Count().Should().Be(5);
            sutCable2WalkOfInterestAfterConduitMove.First().Should().Be(ConduitTestUtilityNetwork.N2);
            sutCable2WalkOfInterestAfterConduitMove.Last().Should().Be(ConduitTestUtilityNetwork.N5);

            sutCable2.NodesOfInterestIds.First().Should().Be(sutCable2WalkOfInterestAfterConduitMove.First());
            sutCable2.NodesOfInterestIds.Last().Should().Be(sutCable2WalkOfInterestAfterConduitMove.Last());

            // Check that hop got new to node id
            sutCable2.UtilityNetworkHops.Last().FromNodeId.Should().Be(ConduitTestUtilityNetwork.N2);
            sutCable2.UtilityNetworkHops.Last().ToNodeId.Should().Be(ConduitTestUtilityNetwork.N1);
        }

        [Fact, Order(10)]
        public async Task Extent3x10Conduit_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutMultiConduitId = ConduitTestUtilityNetwork.Conduit_3x10_N1_N3;
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutMultiConduitId, out var sutMultiConduit);

            var sutCustomerConduitId = ConduitTestUtilityNetwork.Conduit_Single_N3_N7;
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutCustomerConduitId, out var sutCustomerConduit);

            // Connect inner conduit 2 with customer conduit
            var connectCmd = new ConnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: ConduitTestUtilityNetwork.N3,
                spanSegmentsToConnect: new Guid[] {
                    sutMultiConduit.SpanStructures[2].SpanSegments[0].Id,
                    sutCustomerConduit.SpanStructures[0].SpanSegments[0].Id
                }
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd);
            connectResult.IsSuccess.Should().BeTrue();

            // Add cable to conduits
            var sutCable1 = _conduitTestUtilityNetwork.PlaceCableDirectlyInRouteNetwork("K8325_10", TestSpecifications.FiberCable_12Fiber,
                //new Guid[] { ConduitTestUtilityNetwork.S1, ConduitTestUtilityNetwork.S2, ConduitTestUtilityNetwork.S3, ConduitTestUtilityNetwork.S10, ConduitTestUtilityNetwork.S6 });
                new Guid[] { ConduitTestUtilityNetwork.S6, 
                    ConduitTestUtilityNetwork.S10, 
                    ConduitTestUtilityNetwork.S3, 
                    ConduitTestUtilityNetwork.S2,
                    ConduitTestUtilityNetwork.S1 });

            // Affix cable 1 into Conduit N1_N2_4
            var cable1AfterFirstAffix = _conduitTestUtilityNetwork.AffixCableToSingleConduit(ConduitTestUtilityNetwork.N2, sutCable1.Id, sutCustomerConduitId);

            // Check cable after affix
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutCable1.Id, out var cable1AfterAffix);

            var sutCableWalkOfInterestAfterAffix = _conduitTestUtilityNetwork.GetWalkOfInterest(sutCable1.WalkOfInterestId);
            sutCableWalkOfInterestAfterAffix.Count().Should().Be(11);
            sutCableWalkOfInterestAfterAffix.First().Should().Be(ConduitTestUtilityNetwork.N7);
            sutCableWalkOfInterestAfterAffix.Last().Should().Be(ConduitTestUtilityNetwork.N1);

            cable1AfterAffix.NodesOfInterestIds.First().Should().Be(sutCableWalkOfInterestAfterAffix.First());
            cable1AfterAffix.NodesOfInterestIds.Last().Should().Be(sutCableWalkOfInterestAfterAffix.Last());

            cable1AfterAffix.UtilityNetworkHops.Length.Should().Be(1);
            cable1AfterAffix.UtilityNetworkHops.Last().ParentAffixes.Length.Should().Be(2);
            cable1AfterAffix.UtilityNetworkHops.Last().FromNodeId.Should().Be(ConduitTestUtilityNetwork.N8);
            cable1AfterAffix.UtilityNetworkHops.Last().ToNodeId.Should().Be(ConduitTestUtilityNetwork.N1);

            // Extend multi conduit to N5 og N6
            var moveCmd = new MoveSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), sutMultiConduitId, 
                new RouteNetworkElementIdList() { ConduitTestUtilityNetwork.S2, ConduitTestUtilityNetwork.S1, ConduitTestUtilityNetwork.S7, ConduitTestUtilityNetwork.S4 });

            var moveCmdResult = await _commandDispatcher.HandleAsync<MoveSpanEquipment, Result>(moveCmd);

            moveCmdResult.IsSuccess.Should().BeTrue();

            // Check cable after conduit move
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutCable1.Id, out var cableAfterConduitMove);

            var sutCableWalkOfInterestAfterConduitMove = _conduitTestUtilityNetwork.GetWalkOfInterest(sutCable1.WalkOfInterestId);
            sutCableWalkOfInterestAfterConduitMove.Count().Should().Be(15);
            sutCableWalkOfInterestAfterConduitMove.First().Should().Be(ConduitTestUtilityNetwork.N7);
            sutCableWalkOfInterestAfterConduitMove.Last().Should().Be(ConduitTestUtilityNetwork.N6);

            cableAfterConduitMove.NodesOfInterestIds.First().Should().Be(sutCableWalkOfInterestAfterConduitMove.First());
            cableAfterConduitMove.NodesOfInterestIds.Last().Should().Be(sutCableWalkOfInterestAfterConduitMove.Last());

            // Check hop of cable
            cableAfterConduitMove.UtilityNetworkHops.Length.Should().Be(1);
            cableAfterConduitMove.UtilityNetworkHops.Last().ParentAffixes.Length.Should().Be(2);
            cableAfterConduitMove.UtilityNetworkHops.Last().FromNodeId.Should().Be(ConduitTestUtilityNetwork.N8);
            cableAfterConduitMove.UtilityNetworkHops.Last().ToNodeId.Should().Be(ConduitTestUtilityNetwork.N6);
        }

        [Fact, Order(11)]
        public async Task Shrink3x10Conduit_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutMultiConduitId = ConduitTestUtilityNetwork.Conduit_3x10_N1_N3;
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutMultiConduitId, out var sutMultiConduit);

            var sutCustomerConduitId = ConduitTestUtilityNetwork.Conduit_Single_N3_N7;
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutCustomerConduitId, out var sutCustomerConduit);

            // Shrink multi conduit to N5
            var moveCmd = new MoveSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), sutMultiConduitId,
                new RouteNetworkElementIdList() { ConduitTestUtilityNetwork.S2, ConduitTestUtilityNetwork.S1, ConduitTestUtilityNetwork.S7 });

            var moveCmdResult = await _commandDispatcher.HandleAsync<MoveSpanEquipment, Result>(moveCmd);

            moveCmdResult.IsSuccess.Should().BeTrue();

            // Check cable after conduit move
            var sutCabel1Id = utilityNetwork.RelatedCablesByConduitSegmentId[sutCustomerConduit.SpanStructures.First().SpanSegments.First().Id].First();

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutCabel1Id, out var cableConduitMove);

           

            var sutCableWalkOfInterestAfterConduitMove = _conduitTestUtilityNetwork.GetWalkOfInterest(cableConduitMove.WalkOfInterestId);
            sutCableWalkOfInterestAfterConduitMove.Count().Should().Be(13);
            sutCableWalkOfInterestAfterConduitMove.First().Should().Be(ConduitTestUtilityNetwork.N7);
            sutCableWalkOfInterestAfterConduitMove.Last().Should().Be(ConduitTestUtilityNetwork.N5);

            cableConduitMove.NodesOfInterestIds.First().Should().Be(sutCableWalkOfInterestAfterConduitMove.First());
            cableConduitMove.NodesOfInterestIds.Last().Should().Be(sutCableWalkOfInterestAfterConduitMove.Last());

            // Check hop of cable
            cableConduitMove.UtilityNetworkHops.Length.Should().Be(1);
            cableConduitMove.UtilityNetworkHops.Last().ParentAffixes.Length.Should().Be(2);
            cableConduitMove.UtilityNetworkHops.Last().FromNodeId.Should().Be(ConduitTestUtilityNetwork.N8);
            cableConduitMove.UtilityNetworkHops.Last().ToNodeId.Should().Be(ConduitTestUtilityNetwork.N5);
        }


        [Fact, Order(100)]
        public async Task ExtendFromEndOfConduit_N1_N2_4_ShouldFail()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutConduitId = ConduitTestUtilityNetwork.Conduit_N1_N2_4;

            // Extent conduit include N5
            var moveCmd = new MoveSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), sutConduitId, new RouteNetworkElementIdList() { ConduitTestUtilityNetwork.S7, ConduitTestUtilityNetwork.S1, ConduitTestUtilityNetwork.S2 });

            var moveCmdResult = await _commandDispatcher.HandleAsync<MoveSpanEquipment, Result>(moveCmd);

            moveCmdResult.IsFailed.Should().BeTrue();

        }

        [Fact, Order(101)]
        public async Task MoveMultiConduitToN6_ShouldFail()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutConduitId = ConduitTestUtilityNetwork.Conduit_3x10_N1_N3;

            // move end to N5 (from N3)
            var moveCmd = new MoveSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), sutConduitId,
              new RouteNetworkElementIdList() { ConduitTestUtilityNetwork.S8, ConduitTestUtilityNetwork.S1, ConduitTestUtilityNetwork.S7 });

            var moveCmdResult = await _commandDispatcher.HandleAsync<MoveSpanEquipment, Result>(moveCmd);

            moveCmdResult.IsFailed.Should().BeTrue();

        }

    }
}
