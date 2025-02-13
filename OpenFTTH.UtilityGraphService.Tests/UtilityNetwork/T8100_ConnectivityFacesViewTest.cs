using FluentAssertions;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.EventSourcing;
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
    [Order(8010)]
    public class T8100_ConnectivityFacesViewTests
    {
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;
        private ConduitTestUtilityNetwork _conduitTestUtilityNetwork;

        public T8100_ConnectivityFacesViewTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();

            _conduitTestUtilityNetwork = new ConduitTestUtilityNetwork(_eventStore, _commandDispatcher, _queryDispatcher).Run();
        }

  

        [Fact, Order(1)]
        public async Task QueryConnectivityFacesInCC1_ShouldSucceed()
        {
            // Setup
            var sutRouteNodeId = TestRouteNetwork.CC_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutCable = _conduitTestUtilityNetwork.PlaceCableDirectlyInRouteNetwork("customer cable", TestSpecifications.FiberCable_2Fiber,
                new Guid[] { TestRouteNetwork.S5, TestRouteNetwork.S6, TestRouteNetwork.S8 });

            var cableAfterFirstAffix = _conduitTestUtilityNetwork.AffixCableToSingleConduit(sutRouteNodeId, sutCable.Id, TestUtilityNetwork.CustomerConduit_CC_1_to_SDU_2);



            // Get faces
            var connectivityFaceQuery = new GetConnectivityFaces(sutRouteNodeId);

            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetConnectivityFaces, Result<List<ConnectivityFace>>>(
                connectivityFaceQuery
            );


            // Check faces
            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var connectivityFaces = connectivityQueryResult.Value;

            connectivityFaces.Count(f => f.EquipmentKind == ConnectivityEquipmentKindEnum.TerminalEquipment).Should().BeGreaterThan(0);
            connectivityFaces.Count(f => f.EquipmentKind == ConnectivityEquipmentKindEnum.SpanEquipment).Should().BeGreaterThan(0);
            
            connectivityFaces.Should().Contain(s => s.FaceName == "Mod Vesterbrogade 7A");


            // Check face connections for terminal equipment
            var terminalEquipmentFace = connectivityFaces.First(f => f.EquipmentKind == ConnectivityEquipmentKindEnum.TerminalEquipment);

            var terminalEquipmentConnectionsQuery = new GetConnectivityFaceConnections(sutRouteNodeId, terminalEquipmentFace.EquipmentId, terminalEquipmentFace.FaceKind);

            var terminalEquipmentConnectionsQueryResult = await _queryDispatcher.HandleAsync<GetConnectivityFaceConnections, Result<List<ConnectivityFaceConnection>>>(
                terminalEquipmentConnectionsQuery
            );
            terminalEquipmentConnectionsQueryResult.IsSuccess.Should().BeTrue();

            var terminalEquipmentConnections = terminalEquipmentConnectionsQueryResult.Value;

            terminalEquipmentConnections.Count.Should().BeGreaterThan(4);


            // Check face connections for span equipment
            var spanEquipmentFace = connectivityFaces.First(f => f.EquipmentName.StartsWith("K69373563"));

            var spanEquipmentConnectionsQuery = new GetConnectivityFaceConnections(sutRouteNodeId, spanEquipmentFace.EquipmentId, spanEquipmentFace.FaceKind);

            var spanEquipmentConnectionsQueryResult = await _queryDispatcher.HandleAsync<GetConnectivityFaceConnections, Result<List<ConnectivityFaceConnection>>>(
                spanEquipmentConnectionsQuery
            );

            spanEquipmentConnectionsQueryResult.IsSuccess.Should().BeTrue();

            var spanEquipmentConnections = spanEquipmentConnectionsQueryResult.Value;

            spanEquipmentConnections.Count.Should().BeGreaterThan(4);
        }



        [Fact, Order(2)]
        public async Task QueryConnectivityFacesInCO1_ShouldSucceed()
        {
            // Setup
            var sutRouteNodeId = TestRouteNetwork.CO_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();


            // Get faces
            var connectivityFaceQuery = new GetConnectivityFaces(sutRouteNodeId);

            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetConnectivityFaces, Result<List<ConnectivityFace>>>(
                connectivityFaceQuery
            );


            // Check faces
            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var connectivityFaces = connectivityQueryResult.Value;


            // Check splitter holder face
            var terminalEquipmentFace = connectivityFaces.First(f => f.EquipmentName.StartsWith("Splitter"));

            var terminalEquipmentConnectionsQuery = new GetConnectivityFaceConnections(sutRouteNodeId, terminalEquipmentFace.EquipmentId, terminalEquipmentFace.FaceKind);

            var terminalEquipmentConnectionsQueryResult = await _queryDispatcher.HandleAsync<GetConnectivityFaceConnections, Result<List<ConnectivityFaceConnection>>>(
                terminalEquipmentConnectionsQuery
            );
            terminalEquipmentConnectionsQueryResult.IsSuccess.Should().BeTrue();

            var terminalEquipmentConnections = terminalEquipmentConnectionsQueryResult.Value;

            // Splitter out 4 (terminal index 4) should not be connected
            terminalEquipmentConnections[4].IsConnected.Should().BeFalse();

            // Splitter out 5 (terminal index 5) should be connected
            terminalEquipmentConnections[5].IsConnected.Should().BeTrue();

        }


    }
}
