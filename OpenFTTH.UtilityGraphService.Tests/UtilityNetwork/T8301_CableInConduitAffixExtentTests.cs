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
    [Order(8250)]
    public class T8301_CableInConduitAffixExtentTexts
    {
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;
        private ConduitTestUtilityNetwork _conduitTestUtilityNetwork;

        public T8301_CableInConduitAffixExtentTexts(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            _conduitTestUtilityNetwork = new ConduitTestUtilityNetwork(_eventStore, _commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public void ExtentS2CableIntoS1_ShouldSucceed()
        {
            var sutCable = _conduitTestUtilityNetwork.PlaceCableDirectlyInRouteNetwork("K10", TestSpecifications.FiberCable_12Fiber,
                new Guid[] { ConduitTestUtilityNetwork.S2 });

            var cableWoiBeforeAfix = _conduitTestUtilityNetwork.GetWalkOfInterest(sutCable.WalkOfInterestId);
            cableWoiBeforeAfix.Count().Should().Be(3);
            cableWoiBeforeAfix[0].Should().Be(ConduitTestUtilityNetwork.N2);
            cableWoiBeforeAfix[1].Should().Be(ConduitTestUtilityNetwork.S2);
            cableWoiBeforeAfix[2].Should().Be(ConduitTestUtilityNetwork.N3);
            
            /////////////////////////////////
            // Extent cable into Conduit N1_N2_1
            var cableAfterFirstAffix = _conduitTestUtilityNetwork.AffixCableToSingleConduit(ConduitTestUtilityNetwork.N2, sutCable.Id, ConduitTestUtilityNetwork.Conduit_N1_N2_1);
            
            // Cable woi should now be extended to include s1
            var cableWoiAfterFirstAffix = _conduitTestUtilityNetwork.GetWalkOfInterest(sutCable.WalkOfInterestId);
            cableWoiAfterFirstAffix.Count().Should().Be(5);
            cableWoiAfterFirstAffix[0].Should().Be(ConduitTestUtilityNetwork.N1);
            cableWoiAfterFirstAffix[1].Should().Be(ConduitTestUtilityNetwork.S1);
            cableWoiAfterFirstAffix[2].Should().Be(ConduitTestUtilityNetwork.N2);
            cableWoiAfterFirstAffix[3].Should().Be(ConduitTestUtilityNetwork.S2);
            cableWoiAfterFirstAffix[4].Should().Be(ConduitTestUtilityNetwork.N3);

            // Cable node of interst id should now reflect new extent
            cableAfterFirstAffix.NodesOfInterestIds[0].Should().Be(ConduitTestUtilityNetwork.N1);
            cableAfterFirstAffix.NodesOfInterestIds[1].Should().Be(ConduitTestUtilityNetwork.N3);

            // Cable should contain one hop
            cableAfterFirstAffix.UtilityNetworkHops.Should().NotBeNull();
            cableAfterFirstAffix.UtilityNetworkHops.Count().Should().Be(1);
            cableAfterFirstAffix.UtilityNetworkHops[0].FromNodeId.Should().Be(ConduitTestUtilityNetwork.N1);
            cableAfterFirstAffix.UtilityNetworkHops[0].ToNodeId.Should().Be(ConduitTestUtilityNetwork.N2);

            /////////////////////////////////
            // Extent cable into Conduit N3_N4_1
            var cableAfterThirdAffix = _conduitTestUtilityNetwork.AffixCableToSingleConduit(ConduitTestUtilityNetwork.N2, sutCable.Id, ConduitTestUtilityNetwork.Conduit_N3_N4_1);

            // Cable woi should not extended to include s1
            var cableWoiAfterThirdAffix = _conduitTestUtilityNetwork.GetWalkOfInterest(sutCable.WalkOfInterestId);
            cableWoiAfterThirdAffix.Count().Should().Be(7);
            cableWoiAfterThirdAffix[0].Should().Be(ConduitTestUtilityNetwork.N1);
            cableWoiAfterThirdAffix[1].Should().Be(ConduitTestUtilityNetwork.S1);
            cableWoiAfterThirdAffix[2].Should().Be(ConduitTestUtilityNetwork.N2);
            cableWoiAfterThirdAffix[3].Should().Be(ConduitTestUtilityNetwork.S2);
            cableWoiAfterThirdAffix[4].Should().Be(ConduitTestUtilityNetwork.N3);
            cableWoiAfterThirdAffix[5].Should().Be(ConduitTestUtilityNetwork.S3);
            cableWoiAfterThirdAffix[6].Should().Be(ConduitTestUtilityNetwork.N4);

            // Cable node of interst id should now reflect new extent
            cableAfterThirdAffix.NodesOfInterestIds[0].Should().Be(ConduitTestUtilityNetwork.N1);
            cableAfterThirdAffix.NodesOfInterestIds[1].Should().Be(ConduitTestUtilityNetwork.N4);

            // Cable should contain one hop
            cableAfterThirdAffix.UtilityNetworkHops.Should().NotBeNull();
            cableAfterThirdAffix.UtilityNetworkHops.Count().Should().Be(2);
            cableAfterThirdAffix.UtilityNetworkHops[0].FromNodeId.Should().Be(ConduitTestUtilityNetwork.N1);
            cableAfterThirdAffix.UtilityNetworkHops[0].ToNodeId.Should().Be(ConduitTestUtilityNetwork.N2);
            cableAfterThirdAffix.UtilityNetworkHops[1].FromNodeId.Should().Be(ConduitTestUtilityNetwork.N3);
            cableAfterThirdAffix.UtilityNetworkHops[1].ToNodeId.Should().Be(ConduitTestUtilityNetwork.N4);

            /////////////////////////////////
            // Extent cable into Conduit N2_N4_1
            var cableAfterForthAffix = _conduitTestUtilityNetwork.AffixCableToSingleConduit(ConduitTestUtilityNetwork.N2, sutCable.Id, ConduitTestUtilityNetwork.Conduit_N2_N4_1);

            // Cable woi should not extended to include s1
            var cableWoiAfterForthAffix = _conduitTestUtilityNetwork.GetWalkOfInterest(sutCable.WalkOfInterestId);
            cableWoiAfterForthAffix.Count().Should().Be(11);
            cableWoiAfterForthAffix[0].Should().Be(ConduitTestUtilityNetwork.N1);
            cableWoiAfterForthAffix[1].Should().Be(ConduitTestUtilityNetwork.S1);
            cableWoiAfterForthAffix[2].Should().Be(ConduitTestUtilityNetwork.N2);
            cableWoiAfterForthAffix[3].Should().Be(ConduitTestUtilityNetwork.S2);
            cableWoiAfterForthAffix[4].Should().Be(ConduitTestUtilityNetwork.N3);
            cableWoiAfterForthAffix[5].Should().Be(ConduitTestUtilityNetwork.S3);
            cableWoiAfterForthAffix[6].Should().Be(ConduitTestUtilityNetwork.N4);
            cableWoiAfterForthAffix[7].Should().Be(ConduitTestUtilityNetwork.S3);
            cableWoiAfterForthAffix[8].Should().Be(ConduitTestUtilityNetwork.N3);
            cableWoiAfterForthAffix[9].Should().Be(ConduitTestUtilityNetwork.S2);
            cableWoiAfterForthAffix[10].Should().Be(ConduitTestUtilityNetwork.N2);

            // Cable node of interst id should now reflect new extent
            cableAfterForthAffix.NodesOfInterestIds[0].Should().Be(ConduitTestUtilityNetwork.N1);
            cableAfterForthAffix.NodesOfInterestIds[1].Should().Be(ConduitTestUtilityNetwork.N2);


            // Cable should contain one hop
            cableAfterForthAffix.UtilityNetworkHops.Should().NotBeNull();
            cableAfterForthAffix.UtilityNetworkHops.Count().Should().Be(3);
            cableAfterForthAffix.UtilityNetworkHops[0].FromNodeId.Should().Be(ConduitTestUtilityNetwork.N1);
            cableAfterForthAffix.UtilityNetworkHops[0].ToNodeId.Should().Be(ConduitTestUtilityNetwork.N2);
            cableAfterForthAffix.UtilityNetworkHops[1].FromNodeId.Should().Be(ConduitTestUtilityNetwork.N3);
            cableAfterForthAffix.UtilityNetworkHops[1].ToNodeId.Should().Be(ConduitTestUtilityNetwork.N4);
            cableAfterForthAffix.UtilityNetworkHops[2].FromNodeId.Should().Be(ConduitTestUtilityNetwork.N4);
            cableAfterForthAffix.UtilityNetworkHops[2].ToNodeId.Should().Be(ConduitTestUtilityNetwork.N2);
        }
    }
}
