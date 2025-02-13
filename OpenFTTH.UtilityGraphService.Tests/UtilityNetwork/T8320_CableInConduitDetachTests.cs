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
    [Order(8320)]
    public class T8320_CableInConduitMoveDetachTests
    {
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;
        private ConduitTestUtilityNetwork _conduitTestUtilityNetwork;

        public T8320_CableInConduitMoveDetachTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            _conduitTestUtilityNetwork = new ConduitTestUtilityNetwork(_eventStore, _commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public async Task DetachCableInConduit_N2_N2_and_N3_N4_InNode_N1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutCable = _conduitTestUtilityNetwork.PlaceCableDirectlyInRouteNetwork("K8320_1", TestSpecifications.FiberCable_12Fiber,
                new Guid[] { ConduitTestUtilityNetwork.S1, ConduitTestUtilityNetwork.S2, ConduitTestUtilityNetwork.S3 });

            utilityNetwork.TryGetEquipment<SpanEquipment>(ConduitTestUtilityNetwork.Conduit_N1_N2_1, out var conduitToDetachFrom);

            var sutConduitId = ConduitTestUtilityNetwork.Conduit_N2_N3_1;

            // Move cable into Conduit N1_N2_1
            var cableAfterFirstAffix = _conduitTestUtilityNetwork.AffixCableToSingleConduit(ConduitTestUtilityNetwork.N2, sutCable.Id, ConduitTestUtilityNetwork.Conduit_N1_N2_1);

            // Move cable into Conduit N3_N4_1
            var cableAfterSecondAffix = _conduitTestUtilityNetwork.AffixCableToSingleConduit(ConduitTestUtilityNetwork.N2, sutCable.Id, ConduitTestUtilityNetwork.Conduit_N3_N4_1);


            var detachConduitFromNodeContainer = new DetachSpanEquipmentFromNodeContainer(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                        cableAfterSecondAffix.Id,
                        routeNodeId: ConduitTestUtilityNetwork.N1
                    );

            utilityNetwork.RelatedCablesByConduitSegmentId[conduitToDetachFrom.SpanStructures.First().SpanSegments.First().Id].Should().Contain(sutCable.Id);

            // Act
            var detachResult = await _commandDispatcher.HandleAsync<DetachSpanEquipmentFromNodeContainer, Result>(detachConduitFromNodeContainer);

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutCable.Id, out var cableAfterDetach);

            detachResult.IsSuccess.Should().BeTrue();

            var sutCableWalkOfInterestAfterConduitMove = _conduitTestUtilityNetwork.GetWalkOfInterest(sutCable.WalkOfInterestId);

            sutCableWalkOfInterestAfterConduitMove.Count.Should().Be(7);
            cableAfterDetach.UtilityNetworkHops.Length.Should().Be(1);

            utilityNetwork.RelatedCablesByConduitSegmentId[conduitToDetachFrom.SpanStructures.First().SpanSegments.First().Id].Should().NotContain(sutCable.Id);

        }

       

    }
}
