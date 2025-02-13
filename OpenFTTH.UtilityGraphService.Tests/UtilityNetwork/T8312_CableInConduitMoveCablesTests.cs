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
    [Order(8312)]
    public class T8312_CableInConduitMoveCablesTests
    {
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;
        private ConduitTestUtilityNetwork _conduitTestUtilityNetwork;

        public T8312_CableInConduitMoveCablesTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            _conduitTestUtilityNetwork = new ConduitTestUtilityNetwork(_eventStore, _commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public async Task MoveCableInConduit_N2_N2_and_N3_N4_To_To_S8_S5_S9_ShouldSucceed()
        {
            var sutCable = _conduitTestUtilityNetwork.PlaceCableDirectlyInRouteNetwork("K8312_1", TestSpecifications.FiberCable_12Fiber,
                new Guid[] { ConduitTestUtilityNetwork.S1, ConduitTestUtilityNetwork.S2, ConduitTestUtilityNetwork.S3 });

            var sutConduitId = ConduitTestUtilityNetwork.Conduit_N2_N3_1;

            // Move cable into Conduit N1_N2_1
            var cableAfterFirstAffix = _conduitTestUtilityNetwork.AffixCableToSingleConduit(ConduitTestUtilityNetwork.N2, sutCable.Id, ConduitTestUtilityNetwork.Conduit_N1_N2_1);

            // Move cable into Conduit N3_N4_1
            var cableAfterSecondAffix = _conduitTestUtilityNetwork.AffixCableToSingleConduit(ConduitTestUtilityNetwork.N2, sutCable.Id, ConduitTestUtilityNetwork.Conduit_N3_N4_1);

            var moveCmd = new MoveSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), sutCable.Id, new RouteNetworkElementIdList() { ConduitTestUtilityNetwork.S1, ConduitTestUtilityNetwork.S8, ConduitTestUtilityNetwork.S5, ConduitTestUtilityNetwork.S9, ConduitTestUtilityNetwork.S3 });

            var moveCmdResult = await _commandDispatcher.HandleAsync<MoveSpanEquipment, Result>(moveCmd);

            moveCmdResult.IsSuccess.Should().BeTrue();

            var sutCableWalkOfInterestAfterConduitMove = _conduitTestUtilityNetwork.GetWalkOfInterest(sutCable.WalkOfInterestId);

            sutCableWalkOfInterestAfterConduitMove.Count.Should().Be(11);

            sutCableWalkOfInterestAfterConduitMove[0].Should().Be(ConduitTestUtilityNetwork.N1);
            sutCableWalkOfInterestAfterConduitMove[1].Should().Be(ConduitTestUtilityNetwork.S1);
            sutCableWalkOfInterestAfterConduitMove[2].Should().Be(ConduitTestUtilityNetwork.N2);
            sutCableWalkOfInterestAfterConduitMove[3].Should().Be(ConduitTestUtilityNetwork.S8);
            sutCableWalkOfInterestAfterConduitMove[4].Should().Be(ConduitTestUtilityNetwork.N6);
            sutCableWalkOfInterestAfterConduitMove[5].Should().Be(ConduitTestUtilityNetwork.S5);
            sutCableWalkOfInterestAfterConduitMove[6].Should().Be(ConduitTestUtilityNetwork.N7);
            sutCableWalkOfInterestAfterConduitMove[7].Should().Be(ConduitTestUtilityNetwork.S9);
            sutCableWalkOfInterestAfterConduitMove[8].Should().Be(ConduitTestUtilityNetwork.N3);
            sutCableWalkOfInterestAfterConduitMove[9].Should().Be(ConduitTestUtilityNetwork.S3);
            sutCableWalkOfInterestAfterConduitMove[10].Should().Be(ConduitTestUtilityNetwork.N4);
        }

        [Fact, Order(2)]
        public async Task MoveCableInConduit_N1_N2_and_N3_N4_To_S7_S4_S8_ShouldFail()
        {
            var sutCable = _conduitTestUtilityNetwork.PlaceCableDirectlyInRouteNetwork("K8312_2", TestSpecifications.FiberCable_12Fiber,
                new Guid[] { ConduitTestUtilityNetwork.S1, ConduitTestUtilityNetwork.S2, ConduitTestUtilityNetwork.S3 });

            var sutConduitId = ConduitTestUtilityNetwork.Conduit_N2_N3_1;

            // Affix cable into Conduit N1_N2_1
            var cableAfterFirstAffix = _conduitTestUtilityNetwork.AffixCableToSingleConduit(ConduitTestUtilityNetwork.N2, sutCable.Id, ConduitTestUtilityNetwork.Conduit_N1_N2_1);

            // Affix cable into Conduit N3_N4_1
            var cableAfterSecondAffix = _conduitTestUtilityNetwork.AffixCableToSingleConduit(ConduitTestUtilityNetwork.N2, sutCable.Id, ConduitTestUtilityNetwork.Conduit_N3_N4_1);

            var moveCmd = new MoveSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), sutCable.Id, new RouteNetworkElementIdList() { ConduitTestUtilityNetwork.S7, ConduitTestUtilityNetwork.S4, ConduitTestUtilityNetwork.S8, ConduitTestUtilityNetwork.S2, ConduitTestUtilityNetwork.S3 });

            var moveCmdResult = await _commandDispatcher.HandleAsync<MoveSpanEquipment, Result>(moveCmd);

            moveCmdResult.IsFailed.Should().BeTrue();

            var sutCableWalkOfInterestAfterConduitMove = _conduitTestUtilityNetwork.GetWalkOfInterest(sutCable.WalkOfInterestId);

            sutCableWalkOfInterestAfterConduitMove.Count.Should().Be(7);
        }


    }
}
