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
    [Order(8300)]
    public class T8300_CableInConduitAffixTexts
    {
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;
        private ConduitTestUtilityNetwork _conduitTestUtilityNetwork;

        public T8300_CableInConduitAffixTexts(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            _conduitTestUtilityNetwork = new ConduitTestUtilityNetwork(_eventStore, _commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public void AffixCableToConduitNotAffectingWoi_ShouldSucceed()
        {
            var sutCable = _conduitTestUtilityNetwork.PlaceCableDirectlyInRouteNetwork("K1", TestSpecifications.FiberCable_12Fiber,
                new Guid[] { ConduitTestUtilityNetwork.S1, ConduitTestUtilityNetwork.S2, ConduitTestUtilityNetwork.S3 });

            var cableWoiBeforeAfix = _conduitTestUtilityNetwork.GetWalkOfInterest(sutCable.WalkOfInterestId);
            cableWoiBeforeAfix.Count().Should().Be(7);
            cableWoiBeforeAfix[0].Should().Be(ConduitTestUtilityNetwork.N1);
            cableWoiBeforeAfix[1].Should().Be(ConduitTestUtilityNetwork.S1);
            cableWoiBeforeAfix[2].Should().Be(ConduitTestUtilityNetwork.N2);
            cableWoiBeforeAfix[3].Should().Be(ConduitTestUtilityNetwork.S2);
            cableWoiBeforeAfix[4].Should().Be(ConduitTestUtilityNetwork.N3);
            cableWoiBeforeAfix[5].Should().Be(ConduitTestUtilityNetwork.S3);
            cableWoiBeforeAfix[6].Should().Be(ConduitTestUtilityNetwork.N4);

            /////////////////////////////////
            // Move cable into Conduit N2_N3_1
            var cableAfterFirstAffix = _conduitTestUtilityNetwork.AffixCableToSingleConduit(ConduitTestUtilityNetwork.N2, sutCable.Id, ConduitTestUtilityNetwork.Conduit_N2_N3_1);
            
            // Cable woi should not be changed
            var cableWoiAfterFirstAffix = _conduitTestUtilityNetwork.GetWalkOfInterest(sutCable.WalkOfInterestId);
            cableWoiAfterFirstAffix.Should().BeEquivalentTo(cableWoiBeforeAfix);

            // Cable should contain one hop
            cableAfterFirstAffix.UtilityNetworkHops.Should().NotBeNull();
            cableAfterFirstAffix.UtilityNetworkHops.Count().Should().Be(1);
            cableAfterFirstAffix.UtilityNetworkHops[0].FromNodeId.Should().Be(ConduitTestUtilityNetwork.N2);
            cableAfterFirstAffix.UtilityNetworkHops[0].ToNodeId.Should().Be(ConduitTestUtilityNetwork.N3);

            /////////////////////////////////
            // Move cable into Conduit N1_N2_1
            var cableAfterSecondAffix = _conduitTestUtilityNetwork.AffixCableToSingleConduit(ConduitTestUtilityNetwork.N2, sutCable.Id, ConduitTestUtilityNetwork.Conduit_N1_N2_1);

            // Cable woi should not be changed
            var cableWoiAfterSecondAffix = _conduitTestUtilityNetwork.GetWalkOfInterest(sutCable.WalkOfInterestId);
            cableWoiAfterSecondAffix.Should().BeEquivalentTo(cableWoiBeforeAfix);

            // Cable should contain two hops now
            cableAfterSecondAffix.UtilityNetworkHops.Should().NotBeNull();
            cableAfterSecondAffix.UtilityNetworkHops.Count().Should().Be(2);
            cableAfterSecondAffix.UtilityNetworkHops[0].FromNodeId.Should().Be(ConduitTestUtilityNetwork.N1);
            cableAfterSecondAffix.UtilityNetworkHops[0].ToNodeId.Should().Be(ConduitTestUtilityNetwork.N2);
            cableAfterSecondAffix.UtilityNetworkHops[1].FromNodeId.Should().Be(ConduitTestUtilityNetwork.N2);
            cableAfterSecondAffix.UtilityNetworkHops[1].ToNodeId.Should().Be(ConduitTestUtilityNetwork.N3);


            /////////////////////////////////
            // Move cable into Conduit N3_N4_1
            var cableAfterThirdAffix = _conduitTestUtilityNetwork.AffixCableToSingleConduit(ConduitTestUtilityNetwork.N3, sutCable.Id, ConduitTestUtilityNetwork.Conduit_N3_N4_1);

            // Cable woi should not be changed
            var cableWoiAfterThirdAffix = _conduitTestUtilityNetwork.GetWalkOfInterest(sutCable.WalkOfInterestId);
            cableWoiAfterThirdAffix.Should().BeEquivalentTo(cableWoiBeforeAfix);

            // Cable should contain two hops now
            cableAfterThirdAffix.UtilityNetworkHops.Should().NotBeNull();
            cableAfterThirdAffix.UtilityNetworkHops.Count().Should().Be(3);
            cableAfterThirdAffix.UtilityNetworkHops[0].FromNodeId.Should().Be(ConduitTestUtilityNetwork.N1);
            cableAfterThirdAffix.UtilityNetworkHops[0].ToNodeId.Should().Be(ConduitTestUtilityNetwork.N2);
            cableAfterThirdAffix.UtilityNetworkHops[1].FromNodeId.Should().Be(ConduitTestUtilityNetwork.N2);
            cableAfterThirdAffix.UtilityNetworkHops[1].ToNodeId.Should().Be(ConduitTestUtilityNetwork.N3);
            cableAfterThirdAffix.UtilityNetworkHops[2].FromNodeId.Should().Be(ConduitTestUtilityNetwork.N3);
            cableAfterThirdAffix.UtilityNetworkHops[2].ToNodeId.Should().Be(ConduitTestUtilityNetwork.N4);

        }

        [Fact, Order(2)]
        public void AffixCableToConduit_N2_N4_2_AffectingWoi_ShouldSucceed()
        {
            var sutCable = _conduitTestUtilityNetwork.PlaceCableDirectlyInRouteNetwork("K2", TestSpecifications.FiberCable_12Fiber,
                new Guid[] { ConduitTestUtilityNetwork.S1, ConduitTestUtilityNetwork.S2, ConduitTestUtilityNetwork.S3 });

            var cableWoiBeforeAfix = _conduitTestUtilityNetwork.GetWalkOfInterest(sutCable.WalkOfInterestId);
            cableWoiBeforeAfix.Count().Should().Be(7);
            cableWoiBeforeAfix[0].Should().Be(ConduitTestUtilityNetwork.N1);
            cableWoiBeforeAfix[1].Should().Be(ConduitTestUtilityNetwork.S1);
            cableWoiBeforeAfix[2].Should().Be(ConduitTestUtilityNetwork.N2);
            cableWoiBeforeAfix[3].Should().Be(ConduitTestUtilityNetwork.S2);
            cableWoiBeforeAfix[4].Should().Be(ConduitTestUtilityNetwork.N3);
            cableWoiBeforeAfix[5].Should().Be(ConduitTestUtilityNetwork.S3);
            cableWoiBeforeAfix[6].Should().Be(ConduitTestUtilityNetwork.N4);

            /////////////////////////////////
            // Move cable into Conduit N2_N4_2
            var cableAfterFirstAffix = _conduitTestUtilityNetwork.AffixCableToSingleConduit(ConduitTestUtilityNetwork.N2, sutCable.Id, ConduitTestUtilityNetwork.Conduit_N2_N4_2);

            // Cable woi should be changed
            var cableWoiAfterFirstAffix = _conduitTestUtilityNetwork.GetWalkOfInterest(sutCable.WalkOfInterestId);
            cableWoiAfterFirstAffix.Should().NotBeEquivalentTo(cableWoiBeforeAfix);

            // Cable should contain one hop
            cableAfterFirstAffix.UtilityNetworkHops.Should().NotBeNull();
            cableAfterFirstAffix.UtilityNetworkHops.Count().Should().Be(1);
            cableAfterFirstAffix.UtilityNetworkHops[0].FromNodeId.Should().Be(ConduitTestUtilityNetwork.N2);
            cableAfterFirstAffix.UtilityNetworkHops[0].ToNodeId.Should().Be(ConduitTestUtilityNetwork.N4);

            cableWoiAfterFirstAffix.Count().Should().Be(11);
            cableWoiAfterFirstAffix[0].Should().Be(ConduitTestUtilityNetwork.N1);
            cableWoiAfterFirstAffix[1].Should().Be(ConduitTestUtilityNetwork.S1);
            cableWoiAfterFirstAffix[2].Should().Be(ConduitTestUtilityNetwork.N2);
            cableWoiAfterFirstAffix[3].Should().Be(ConduitTestUtilityNetwork.S8);
            cableWoiAfterFirstAffix[4].Should().Be(ConduitTestUtilityNetwork.N6);
            cableWoiAfterFirstAffix[5].Should().Be(ConduitTestUtilityNetwork.S5);
            cableWoiAfterFirstAffix[6].Should().Be(ConduitTestUtilityNetwork.N7);
            cableWoiAfterFirstAffix[7].Should().Be(ConduitTestUtilityNetwork.S6);
            cableWoiAfterFirstAffix[8].Should().Be(ConduitTestUtilityNetwork.N8);
            cableWoiAfterFirstAffix[9].Should().Be(ConduitTestUtilityNetwork.S10);
            cableWoiAfterFirstAffix[10].Should().Be(ConduitTestUtilityNetwork.N4);
        }

        [Fact, Order(3)]
        public void AffixCableToConduit_N2_N1_1_AffectingWoi_ShouldSucceed()
        {
            var sutCable = _conduitTestUtilityNetwork.PlaceCableDirectlyInRouteNetwork("K3", TestSpecifications.FiberCable_12Fiber,
                new Guid[] { ConduitTestUtilityNetwork.S1, ConduitTestUtilityNetwork.S2, ConduitTestUtilityNetwork.S3 });

            var cableWoiBeforeAfix = _conduitTestUtilityNetwork.GetWalkOfInterest(sutCable.WalkOfInterestId);
            cableWoiBeforeAfix.Count().Should().Be(7);
            cableWoiBeforeAfix[0].Should().Be(ConduitTestUtilityNetwork.N1);
            cableWoiBeforeAfix[1].Should().Be(ConduitTestUtilityNetwork.S1);
            cableWoiBeforeAfix[2].Should().Be(ConduitTestUtilityNetwork.N2);
            cableWoiBeforeAfix[3].Should().Be(ConduitTestUtilityNetwork.S2);
            cableWoiBeforeAfix[4].Should().Be(ConduitTestUtilityNetwork.N3);
            cableWoiBeforeAfix[5].Should().Be(ConduitTestUtilityNetwork.S3);
            cableWoiBeforeAfix[6].Should().Be(ConduitTestUtilityNetwork.N4);

            /////////////////////////////////
            // Move cable into Conduit N2_N4_2
            var cableAfterFirstAffix = _conduitTestUtilityNetwork.AffixCableToSingleConduit(ConduitTestUtilityNetwork.N2, sutCable.Id, ConduitTestUtilityNetwork.Conduit_N2_N1_1);

            // Cable woi should be changed
            var cableWoiAfterFirstAffix = _conduitTestUtilityNetwork.GetWalkOfInterest(sutCable.WalkOfInterestId);
            cableWoiAfterFirstAffix.Should().NotBeEquivalentTo(cableWoiBeforeAfix);

            // Cable should contain one hop
            cableAfterFirstAffix.UtilityNetworkHops.Should().NotBeNull();
            cableAfterFirstAffix.UtilityNetworkHops.Count().Should().Be(1);
            cableAfterFirstAffix.UtilityNetworkHops[0].FromNodeId.Should().Be(ConduitTestUtilityNetwork.N1);
            cableAfterFirstAffix.UtilityNetworkHops[0].ToNodeId.Should().Be(ConduitTestUtilityNetwork.N2);

            cableWoiAfterFirstAffix.Count().Should().Be(11);
            cableWoiAfterFirstAffix[0].Should().Be(ConduitTestUtilityNetwork.N1);
            cableWoiAfterFirstAffix[1].Should().Be(ConduitTestUtilityNetwork.S7);
            cableWoiAfterFirstAffix[2].Should().Be(ConduitTestUtilityNetwork.N5);
            cableWoiAfterFirstAffix[3].Should().Be(ConduitTestUtilityNetwork.S4);
            cableWoiAfterFirstAffix[4].Should().Be(ConduitTestUtilityNetwork.N6);
            cableWoiAfterFirstAffix[5].Should().Be(ConduitTestUtilityNetwork.S8);
            cableWoiAfterFirstAffix[6].Should().Be(ConduitTestUtilityNetwork.N2);
            cableWoiAfterFirstAffix[7].Should().Be(ConduitTestUtilityNetwork.S2);
            cableWoiAfterFirstAffix[8].Should().Be(ConduitTestUtilityNetwork.N3);
            cableWoiAfterFirstAffix[9].Should().Be(ConduitTestUtilityNetwork.S3);
            cableWoiAfterFirstAffix[10].Should().Be(ConduitTestUtilityNetwork.N4);
        }

        [Fact, Order(3)]
        public void AffixCableRemoveCableAffixAnotherCableToSingleConduit_ShouldSucceed()
        {
            // Create cable 1
            var sutCable1 = _conduitTestUtilityNetwork.PlaceCableDirectlyInRouteNetwork("K1_to_be_removed", TestSpecifications.FiberCable_12Fiber,
                new Guid[] { ConduitTestUtilityNetwork.S6,
                    ConduitTestUtilityNetwork.S10,
                    ConduitTestUtilityNetwork.S3,
                    ConduitTestUtilityNetwork.S2,
                    ConduitTestUtilityNetwork.S1 });

            // Affix 1
            var cable1AfterFirstAffix = _conduitTestUtilityNetwork.AffixCableToSingleConduit(ConduitTestUtilityNetwork.N2, sutCable1.Id, ConduitTestUtilityNetwork.Conduit_Single_N3_N7);

            // Remove cable 1
            var removeStructureCmd = new RemoveSpanStructureFromSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), cable1AfterFirstAffix.SpanStructures[0].SpanSegments[0].Id);

            var removeStructureCmdResult = _commandDispatcher.HandleAsync<RemoveSpanStructureFromSpanEquipment, Result>(removeStructureCmd).Result;

            removeStructureCmdResult.IsSuccess.Should().BeTrue();

            // Create cable 2
            var sutCable2 = _conduitTestUtilityNetwork.PlaceCableDirectlyInRouteNetwork("k2_to_be_removed", TestSpecifications.FiberCable_12Fiber,
                new Guid[] { ConduitTestUtilityNetwork.S6,
                    ConduitTestUtilityNetwork.S10,
                    ConduitTestUtilityNetwork.S3,
                    ConduitTestUtilityNetwork.S2,
                    ConduitTestUtilityNetwork.S1 });


            // Affix 2
            var cableAfterFirstAffix2 = _conduitTestUtilityNetwork.AffixCableToSingleConduit(ConduitTestUtilityNetwork.N2, sutCable2.Id, ConduitTestUtilityNetwork.Conduit_Single_N3_N7);


            // Remove cable 2
            var removeStructureCmd2 = new RemoveSpanStructureFromSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), cableAfterFirstAffix2.SpanStructures[0].SpanSegments[0].Id);

            var removeStructureCmdResult2 = _commandDispatcher.HandleAsync<RemoveSpanStructureFromSpanEquipment, Result>(removeStructureCmd2).Result;

            removeStructureCmdResult2.IsSuccess.Should().BeTrue();


        }



        [Fact, Order(100)]
        public void AffixCableToConduitThatDoesNotAllignWithCableWoi_ShouldFail()
        {
            var sutCable = _conduitTestUtilityNetwork.PlaceCableDirectlyInRouteNetwork("K2", TestSpecifications.FiberCable_12Fiber,
                new Guid[] { ConduitTestUtilityNetwork.S1, ConduitTestUtilityNetwork.S2 });

            // Move cable into Conduit N2_N4 should fail, because cable is going from N1 to N3
            _conduitTestUtilityNetwork.Invoking(y => y.AffixCableToSingleConduit(ConduitTestUtilityNetwork.N2, sutCable.Id, ConduitTestUtilityNetwork.Conduit_N2_N4_1))
                .Should().Throw<ApplicationException>();
        }
    }
}
