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
    [Order(5100)]
    public class T5100_EquipmentConnectivityTests
    {
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;

        public T5100_EquipmentConnectivityTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }

        #region CO-1 Connectivity

        [Fact, Order(1)]
        public async Task ConnectFirstTerminalEquipmentInCO1WithFiberCable_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CO_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;
            var sutCableName = "K69373563";


            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get equipment
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.TerminalEquipmentReferences.First(), out var terminalEquipment);

            // Get cable
            var connectivityQuery = new GetConnectivityFaces(nodeContainer.RouteNodeId);

            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetConnectivityFaces, Result<List<ConnectivityFace>>>(
                connectivityQuery
            );

            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;

            var cableId = viewModel.First(m => m.EquipmentName.StartsWith(sutCableName)).EquipmentId;

            utilityNetwork.TryGetEquipment<SpanEquipment>(cableId, out var cableSpanEquipment);


            // ACT (do the connect between cable and equipment)
            var connectCmd = new ConnectSpanSegmentsWithTerminalsAtRouteNode(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: sutNodeId,
                connects: new ConnectSpanSegmentToTerminalOperation[]
                {
                    // Fiber 2 -> Tray 2 Pin 1
                    new ConnectSpanSegmentToTerminalOperation(cableSpanEquipment.SpanStructures[2].SpanSegments[0].Id, terminalEquipment.TerminalStructures[1].Terminals[0].Id),

                    // Fiber 3 -> Tray 2 Pin 2
                    new ConnectSpanSegmentToTerminalOperation(cableSpanEquipment.SpanStructures[3].SpanSegments[0].Id, terminalEquipment.TerminalStructures[1].Terminals[1].Id),

                    // Fiber 4 -> Tray 2 Pin 3
                    new ConnectSpanSegmentToTerminalOperation(cableSpanEquipment.SpanStructures[4].SpanSegments[0].Id, terminalEquipment.TerminalStructures[1].Terminals[2].Id),

                    // Fiber 5 -> Tray 2 Pin 4
                    new ConnectSpanSegmentToTerminalOperation(cableSpanEquipment.SpanStructures[5].SpanSegments[0].Id, terminalEquipment.TerminalStructures[1].Terminals[3].Id)
                }
            );
            var connectCmdResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsWithTerminalsAtRouteNode, Result>(connectCmd);

            // Assert
            connectCmdResult.IsSuccess.Should().BeTrue();

            // Trace tray 1 fiber 1 (should not be connected to anything)
            var fiber1TraceResult = utilityNetwork.Graph.SimpleTrace(cableSpanEquipment.SpanStructures[1].SpanSegments[0].Id);

            fiber1TraceResult.Upstream.Length.Should().Be(0);
            fiber1TraceResult.Downstream.Length.Should().Be(0);

            // Trace 2
            var fiber2TraceResult = utilityNetwork.Graph.SimpleTrace(cableSpanEquipment.SpanStructures[2].SpanSegments[0].Id);

            var downstreamTerminalFromTrace = fiber2TraceResult.Downstream.First(t => t.Id == terminalEquipment.TerminalStructures[1].Terminals[0].Id) as IUtilityGraphTerminalRef;

            var equipmentFromTracedTerminal = downstreamTerminalFromTrace.TerminalEquipment(utilityNetwork);

            equipmentFromTracedTerminal.Should().Be(terminalEquipment);

            // Trace tray 1 terminal 1
            var term4TraceResult = utilityNetwork.Graph.SimpleTrace(terminalEquipment.TerminalStructures[1].Terminals[0].Id);

            term4TraceResult.Downstream.Length.Should().Be(0);
            term4TraceResult.Upstream.Length.Should().Be(2); // a segment and a terminal at the end
            ((UtilityGraphConnectedTerminal)term4TraceResult.Upstream.Last()).RouteNodeId.Should().NotBeEmpty();



            // Check faces and face connections
            var connectivityFaceQuery = new GetConnectivityFaces(sutNodeId);

            var connectivityFaceQueryResult = await _queryDispatcher.HandleAsync<GetConnectivityFaces, Result<List<ConnectivityFace>>>(
                connectivityFaceQuery
            );

            var spanEquipmentFace = connectivityFaceQueryResult.Value.First(f => f.EquipmentId == cableSpanEquipment.Id);

            // Get face connections for span equipment in CO_1 (where it is spliced)
            var spanEquipmentConnectionsQueryInCO1 = new GetConnectivityFaceConnections(sutNodeId, spanEquipmentFace.EquipmentId, spanEquipmentFace.FaceKind);

            var spanEquipmentConnectionsQueryInCO1Result = await _queryDispatcher.HandleAsync<GetConnectivityFaceConnections, Result<List<ConnectivityFaceConnection>>>(
                spanEquipmentConnectionsQueryInCO1
            );

            spanEquipmentConnectionsQueryInCO1Result.IsSuccess.Should().BeTrue();

            var spanEquipmentConnectionsInCO1 = spanEquipmentConnectionsQueryInCO1Result.Value;

            spanEquipmentConnectionsInCO1[0].IsConnected.Should().BeFalse();
            spanEquipmentConnectionsInCO1[1].IsConnected.Should().BeTrue();
            spanEquipmentConnectionsInCO1[2].IsConnected.Should().BeTrue();
            spanEquipmentConnectionsInCO1[3].IsConnected.Should().BeTrue();
            spanEquipmentConnectionsInCO1[4].IsConnected.Should().BeTrue();
            spanEquipmentConnectionsInCO1[5].IsConnected.Should().BeFalse();
        }

        [Fact, Order(2)]
        public async Task ConnectFirstLisaTrayInODFRackInCO1WithFiberCable_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CO_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;
            var sutCableName = "K69373563";

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get equipment
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[0].SubrackMounts.First().TerminalEquipmentId, out var terminalEquipment);

            // Get cable
            var connectivityQuery = new GetConnectivityFaces(nodeContainer.RouteNodeId);

            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetConnectivityFaces, Result<List<ConnectivityFace>>>(
                connectivityQuery
            );

            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;

            var cableId = viewModel.First(m => m.EquipmentName.StartsWith(sutCableName)).EquipmentId;

            utilityNetwork.TryGetEquipment<SpanEquipment>(cableId, out var cableSpanEquipment);


            // ACT (do the connect between cable and equipment)
            var connectCmd = new ConnectSpanSegmentsWithTerminalsAtRouteNode(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: sutNodeId,
                connects: new ConnectSpanSegmentToTerminalOperation[]
                {
                    // Fiber 12 -> Tray 1 Pin 1
                    new ConnectSpanSegmentToTerminalOperation(cableSpanEquipment.SpanStructures[12].SpanSegments[0].Id, terminalEquipment.TerminalStructures[0].Terminals[0].Id)
                }
            );
            var connectCmdResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsWithTerminalsAtRouteNode, Result>(connectCmd);

            // Assert
            connectCmdResult.IsSuccess.Should().BeTrue();

        }

        [Fact, Order(3)]
        public async Task DisconnectFirstTerminalEquipmentInCO1WithFiberCable_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CO_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;
            var sutCableName = "K69373563";


            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get equipment
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.TerminalEquipmentReferences.First(), out var terminalEquipment);

            // Get cable
            var connectivityQuery = new GetConnectivityFaces(nodeContainer.RouteNodeId);

            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetConnectivityFaces, Result<List<ConnectivityFace>>>(
                connectivityQuery
            );

            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;

            var cableId = viewModel.First(m => m.EquipmentName.StartsWith(sutCableName)).EquipmentId;

            utilityNetwork.TryGetEquipment<SpanEquipment>(cableId, out var cableBeforeDisconnect);

            var beforeTrace = utilityNetwork.Graph.SimpleTrace(cableBeforeDisconnect.SpanStructures[2].SpanSegments[0].Id);
            beforeTrace.All.Any(g => g.Id == terminalEquipment.TerminalStructures[1].Terminals[0].Id).Should().BeTrue();


            // ACT (do the connect between cable and equipment)
            var disconnectCmd = new DisconnectSpanSegmentsFromTerminalsAtRouteNode(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: sutNodeId,
                disconnects: new DisconnectSpanSegmentFromTerminalOperation[]
                {
                    // Fiber 2 -> Tray 2 Pin 1
                    new DisconnectSpanSegmentFromTerminalOperation(cableBeforeDisconnect.SpanStructures[2].SpanSegments[0].Id, terminalEquipment.TerminalStructures[1].Terminals[0].Id),
                    // Fiber 3 -> Tray 2 Pin 2
                    new DisconnectSpanSegmentFromTerminalOperation(cableBeforeDisconnect.SpanStructures[3].SpanSegments[0].Id, terminalEquipment.TerminalStructures[1].Terminals[1].Id)
                }
            );
            var disconnectCmdResult = await _commandDispatcher.HandleAsync<DisconnectSpanSegmentsFromTerminalsAtRouteNode, Result>(disconnectCmd);

            // Assert
            disconnectCmdResult.IsSuccess.Should().BeTrue();

            utilityNetwork.TryGetEquipment<SpanEquipment>(cableId, out var cableAfterDisconnect);

            cableAfterDisconnect.SpanStructures[2].SpanSegments[0].FromTerminalId.Should().BeEmpty();
            cableAfterDisconnect.SpanStructures[3].SpanSegments[0].FromTerminalId.Should().BeEmpty();

            var afterTraceFiber2 = utilityNetwork.Graph.SimpleTrace(cableAfterDisconnect.SpanStructures[2].SpanSegments[0].Id);
            afterTraceFiber2.All.Any(g => g.Id == terminalEquipment.TerminalStructures[1].Terminals[0].Id).Should().BeFalse();

            var afterTraceFiber3 = utilityNetwork.Graph.SimpleTrace(cableAfterDisconnect.SpanStructures[3].SpanSegments[0].Id);
            afterTraceFiber2.All.Any(g => g.Id == terminalEquipment.TerminalStructures[1].Terminals[1].Id).Should().BeFalse();
        }

        [Fact, Order(4)]
        public async Task ConnectFirstTerminalEquipmentInCO1WithFiberCableAgain_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CO_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;
            var sutCableName = "K69373563";


            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get equipment
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.TerminalEquipmentReferences.First(), out var terminalEquipment);

            // Get cable
            var connectivityQuery = new GetConnectivityFaces(nodeContainer.RouteNodeId);

            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetConnectivityFaces, Result<List<ConnectivityFace>>>(
                connectivityQuery
            );

            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;

            var cableId = viewModel.First(m => m.EquipmentName.StartsWith(sutCableName)).EquipmentId;

            utilityNetwork.TryGetEquipment<SpanEquipment>(cableId, out var cableSpanEquipment);


            // ACT (do the connect between cable and equipment)
            var connectCmd = new ConnectSpanSegmentsWithTerminalsAtRouteNode(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: sutNodeId,
                connects: new ConnectSpanSegmentToTerminalOperation[]
                {
                    // Fiber 2 -> Tray 2 Pin 1
                    new ConnectSpanSegmentToTerminalOperation(cableSpanEquipment.SpanStructures[2].SpanSegments[0].Id, terminalEquipment.TerminalStructures[1].Terminals[0].Id),

                    // Fiber 3 -> Tray 2 Pin 2
                    new ConnectSpanSegmentToTerminalOperation(cableSpanEquipment.SpanStructures[3].SpanSegments[0].Id, terminalEquipment.TerminalStructures[1].Terminals[1].Id)
                }
            );
            var connectCmdResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsWithTerminalsAtRouteNode, Result>(connectCmd);

            // Assert
            connectCmdResult.IsSuccess.Should().BeTrue();

            var trace = utilityNetwork.Graph.SimpleTrace(cableSpanEquipment.SpanStructures[2].SpanSegments[0].Id);
            trace.All.Any(g => g.Id == terminalEquipment.TerminalStructures[1].Terminals[0].Id).Should().BeTrue();

        }

        [Fact, Order(5)]
        public async Task Connect1_32SplitterOut5ToLisaTray1Pin1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CO_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;
       
            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get lisa tray 
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[0].SubrackMounts.First().TerminalEquipmentId, out var lisaTray);

            // Get first 1:32 splitter at splitter holder at top of rack
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[0].SubrackMounts.Last().TerminalEquipmentId, out var customerSplitter);

            // Connect customer splitter out 5 to lisa tray
            var connectCmd = new ConnectTerminalsAtRouteNode(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: sutNodeId,
                fromTerminalId: customerSplitter.TerminalStructures.First().Terminals[5].Id,
                toTerminalId: lisaTray.TerminalStructures.First().Terminals.First().Id,
                fiberCoordLength: 100.0
            );

            var connectCmdResult = await _commandDispatcher.HandleAsync<ConnectTerminalsAtRouteNode, Result>(connectCmd);

            // Assert
            connectCmdResult.IsSuccess.Should().BeTrue();

            var trace = utilityNetwork.Graph.SimpleTrace(lisaTray.TerminalStructures.First().Terminals.First().Id);
            
            trace.All.Any(g => g.Id == customerSplitter.TerminalStructures.First().Terminals[5].Id).Should().BeTrue();


            // Check equipment connectivity view on 1:32 splitter
            var connectivityViewQuery = new GetTerminalEquipmentConnectivityView(sutNodeId, customerSplitter.Id);

            var connectivityViewResult = await _queryDispatcher.HandleAsync<GetTerminalEquipmentConnectivityView, Result<TerminalEquipmentAZConnectivityViewModel>>(
                connectivityViewQuery
            );

            connectivityViewResult.IsSuccess.Should().BeTrue();

            var connectivityTraceView = connectivityViewResult.Value.TerminalEquipments.First();

            //connectivityTraceView.Name.Should().Be("CO-1 - ODF - Bakke 1");


        }

        [Fact, Order(5)]
        public async Task Connect1_32SplitterOut6ToLisaTray1Pin2_Reversed_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CO_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get lisa tray 
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[0].SubrackMounts.First().TerminalEquipmentId, out var lisaTray);

            // Get first 1:32 splitter at splitter holder at top of rack
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[0].SubrackMounts.Last().TerminalEquipmentId, out var customerSplitter);

            // Connect customer splitter out 5 to lisa tray
            var connectCmd = new ConnectTerminalsAtRouteNode(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: sutNodeId,
                fromTerminalId: lisaTray.TerminalStructures.First().Terminals[1].Id,
                toTerminalId: customerSplitter.TerminalStructures.First().Terminals[6].Id,
                fiberCoordLength: 100.0
            );

            var connectCmdResult = await _commandDispatcher.HandleAsync<ConnectTerminalsAtRouteNode, Result>(connectCmd);

            // Assert
            connectCmdResult.IsSuccess.Should().BeTrue();

            var trace = utilityNetwork.Graph.SimpleTrace(lisaTray.TerminalStructures.First().Terminals.First().Id);

            trace.All.Any(g => g.Id == customerSplitter.TerminalStructures.First().Terminals[5].Id).Should().BeTrue();
        }

        [Fact, Order(6)]
        public async Task Connect1_2SplitterTo1_32Splitter_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CO_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;
       
            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);


            // Get lisa tray
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[0].SubrackMounts.First().TerminalEquipmentId, out var lisaTray);

            var lisaTray1Pin1 = lisaTray.TerminalStructures.First().Terminals[0].Id;
            var lisaTray1Pin2 = lisaTray.TerminalStructures.First().Terminals[1].Id;


            // Get 1:2 splitter at pos 100
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[0].SubrackMounts.First(s => s.Position == 100).TerminalEquipmentId, out var split1_2);

            var split_1_2_out1 = split1_2.TerminalStructures.First().Terminals[1].Id;

            // Get first 1:32 splitter at splitter holder at top of rack
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[0].SubrackMounts.Last().TerminalEquipmentId, out var split1_32);

            var split_1_32_in = split1_32.TerminalStructures.First().Terminals.First().Id;
            var split_1_32_out5 = split1_32.TerminalStructures.First().Terminals[5].Id;
            var split_1_32_out6 = split1_32.TerminalStructures.First().Terminals[6].Id;


            // Connect 1_2 splitter with 1_32 splitter module 1
            var connectCmd = new ConnectTerminalsAtRouteNode(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: sutNodeId,
                fromTerminalId: split_1_2_out1,
                toTerminalId: split_1_32_in,
                fiberCoordLength: 100.0
            );

            var connectCmdResult = await _commandDispatcher.HandleAsync<ConnectTerminalsAtRouteNode, Result>(connectCmd);

            // Assert
            connectCmdResult.IsSuccess.Should().BeTrue();

            // Try trace from lisa tray 1 pin 1
            var traceFromLisaTray1Pin1 = utilityNetwork.Graph.SimpleTrace(lisaTray1Pin1);
            
            // We should hit both 1_2 and 1_32 splitter out ports
            traceFromLisaTray1Pin1.All.Any(g => g.Id == split_1_2_out1).Should().BeTrue();
            traceFromLisaTray1Pin1.All.Any(g => g.Id == split_1_32_out5).Should().BeTrue();


            // Try trace from lisa tray 1 pin 2
            var traceFromLisaTray1Pin2 = utilityNetwork.Graph.SimpleTrace(lisaTray1Pin2);

            // We should hit both 1_2 and 1_32 splitter out ports
            traceFromLisaTray1Pin2.All.Any(g => g.Id == split_1_2_out1).Should().BeTrue();
            traceFromLisaTray1Pin2.All.Any(g => g.Id == split_1_32_out6).Should().BeTrue();


            // Try trace from 1_32 splitter in port
            var trace1_32_SplitInPort = utilityNetwork.Graph.SimpleTrace(split1_32.TerminalStructures.First().Terminals.First().Id);

            // We should only hit 1_2 splitter out port
            trace1_32_SplitInPort.All.Any(g => g.Id == split_1_2_out1).Should().BeTrue();
            trace1_32_SplitInPort.All.Any(g => g.Id == split_1_32_out5).Should().BeFalse();

            // Test connectivity face cable connected ti lisa tray
            var sutCableName = "K69373563";

            var cable = FindSpanEquipmentRelatedToRouteNetworkElementByName(sutNodeId, sutCableName);

            // Get faces
            var connectivityTrace = new GetSpanEquipmentConnectivityView(sutNodeId, new Guid[] { cable.Id });

            // Act
            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetSpanEquipmentConnectivityView, Result<SpanEquipmentAZConnectivityViewModel>>(
                connectivityTrace
            );

            // Assert
            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;
            var firstSpanEquipment = viewModel.SpanEquipments.First();

            firstSpanEquipment.Lines[11].A.End.Should().Contain("1:2 Split");
        }

        [Fact, Order(7)]
        public async Task ConnectOltToWdm_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CO_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get olt
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[1].SubrackMounts.First(s => s.Position == 30).TerminalEquipmentId, out var olt);

            // Get lgx holder
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[1].SubrackMounts.First(s => s.Position == 10).TerminalEquipmentId, out var lgx);


            var oltCard1Port1 = olt.TerminalStructures.First().Terminals[0].Id;
            var wdmIpPort = lgx.TerminalStructures.First().Terminals[1].Id;

            // Connect olt with wdm
            var connectCmd = new ConnectTerminalsAtRouteNode(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: sutNodeId,
                fromTerminalId: oltCard1Port1,
                toTerminalId: wdmIpPort,
                fiberCoordLength: 100.0
            );

            var connectCmdResult = await _commandDispatcher.HandleAsync<ConnectTerminalsAtRouteNode, Result>(connectCmd);

            // Assert
            connectCmdResult.IsSuccess.Should().BeTrue();


            // Check equipment connectivity view
            var connectivityViewQuery = new GetTerminalEquipmentConnectivityView(sutNodeId, olt.Id);

            var connectivityViewResult = await _queryDispatcher.HandleAsync<GetTerminalEquipmentConnectivityView, Result<TerminalEquipmentAZConnectivityViewModel>>(
                connectivityViewQuery
            );

            connectivityViewResult.IsSuccess.Should().BeTrue();

            var connectivityTraceView = connectivityViewResult.Value;
        }

        [Fact, Order(8)]
        public async Task DisonnectOltFromWdm_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CO_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get olt
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[1].SubrackMounts.First(s => s.Position == 30).TerminalEquipmentId, out var olt);

            // Get lgx holder
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[1].SubrackMounts.First(s => s.Position == 10).TerminalEquipmentId, out var lgx);

            var oltCard1Port1Id = olt.TerminalStructures.First().Terminals[0].Id;
            var wdmIpPortId = lgx.TerminalStructures.First().Terminals[1].Id;

            // Get terminal to terminal connection
            var version = utilityNetwork.Graph.LatestCommitedVersion;
            utilityNetwork.Graph.TryGetGraphElement<UtilityGraphConnectedTerminal>(oltCard1Port1Id, out var oltCard1Port1Terminal);
            var terminalToTerminalConnection = oltCard1Port1Terminal.NeighborElements(version).First(n => n is UtilityGraphTerminalToTerminalConnectivityLink) as UtilityGraphTerminalToTerminalConnectivityLink;


            // Check disconnect view
            var getDisconnectView = new GetDisconnectSpanEquipmentFromTerminalView(terminalToTerminalConnection.Id, oltCard1Port1Id);

            var getDisconnectViewQueryResult = await _queryDispatcher.HandleAsync<GetDisconnectSpanEquipmentFromTerminalView, Result<DisconnectSpanEquipmentFromTerminalView>>(
                getDisconnectView
            );


            // Disconnect olt with wdm
            var disconnectCmd = new DisconnectSpanSegmentsFromTerminalsAtRouteNode(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: sutNodeId,
                disconnects: new DisconnectSpanSegmentFromTerminalOperation[]
                {
                    new DisconnectSpanSegmentFromTerminalOperation(terminalToTerminalConnection.Id, oltCard1Port1Id)
                }
            ); 

            var disconnectCmdResult = await _commandDispatcher.HandleAsync<DisconnectSpanSegmentsFromTerminalsAtRouteNode, Result>(disconnectCmd);

            // Assert
            disconnectCmdResult.IsSuccess.Should().BeTrue();


            // Check equipment connectivity view
            var connectivityViewQuery = new GetTerminalEquipmentConnectivityView(sutNodeId, olt.Id);

            var connectivityViewResult = await _queryDispatcher.HandleAsync<GetTerminalEquipmentConnectivityView, Result<TerminalEquipmentAZConnectivityViewModel>>(
                connectivityViewQuery
            );

            connectivityViewResult.IsSuccess.Should().BeTrue();
            var oltTerminal1line = connectivityViewResult.Value.TerminalEquipments.First().TerminalStructures.First(t => t.Name == "1").Lines.First();

            oltTerminal1line.Z.ConnectedTo.Should().BeNull();

        }

        [Fact, Order(10)]
        public async Task ConnectLisaTray10Pin1ToLisaTray11Pin1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CO_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get lisa tray 10
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[0].SubrackMounts.First(s => s.Position == 10).TerminalEquipmentId, out var lisaTray10);

            // Get lisa tray 11
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[0].SubrackMounts.First(s => s.Position == 11).TerminalEquipmentId, out var lisaTray11);

            // Connect 
            var connectCmd = new ConnectTerminalsAtRouteNode(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: sutNodeId,
                fromTerminalId: lisaTray10.TerminalStructures.First().Terminals.First().Id,
                toTerminalId: lisaTray11.TerminalStructures.First().Terminals.First().Id,
                fiberCoordLength: 100.0
            );

            var connectCmdResult = await _commandDispatcher.HandleAsync<ConnectTerminalsAtRouteNode, Result>(connectCmd);

            // Assert
            connectCmdResult.IsSuccess.Should().BeTrue();
        }

        [Fact, Order(11)]
        public async Task ConnectLisaTray10Pin1ToLisaTray12Pin1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CO_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get lisa tray 10
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[0].SubrackMounts.First(s => s.Position == 10).TerminalEquipmentId, out var lisaTray10);

            // Get lisa tray 12
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[0].SubrackMounts.First(s => s.Position == 12).TerminalEquipmentId, out var lisaTray12);

            // Connect 
            var connectCmd = new ConnectTerminalsAtRouteNode(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: sutNodeId,
                toTerminalId: lisaTray10.TerminalStructures.First().Terminals.First().Id,
                fromTerminalId: lisaTray12.TerminalStructures.First().Terminals.First().Id,
                fiberCoordLength: 100.0
            );

            var connectCmdResult = await _commandDispatcher.HandleAsync<ConnectTerminalsAtRouteNode, Result>(connectCmd);

            // Assert
            connectCmdResult.IsSuccess.Should().BeTrue();
        }



        [Fact, Order(12)]
        public async Task DisonnectLisaTray10Pin1FromLisaTray11Pin1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CO_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get lisa tray 10
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[0].SubrackMounts.First(s => s.Position == 10).TerminalEquipmentId, out var lisaTray10);
            var lisaTray10Port1TerminalId = lisaTray10.TerminalStructures.First().Terminals.First().Id;

            // Get lisa tray 11
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[0].SubrackMounts.First(s => s.Position == 11).TerminalEquipmentId, out var lisaTray11);
            var lisaTray11Port1TerminalId = lisaTray11.TerminalStructures.First().Terminals.First().Id;

            // Get terminal to terminal connection
            var version = utilityNetwork.Graph.LatestCommitedVersion;
            utilityNetwork.Graph.TryGetGraphElement<UtilityGraphConnectedTerminal>(lisaTray10Port1TerminalId, out var lisaTray10Port1Terminal);
            var terminalToTerminalConnection = lisaTray10Port1Terminal.NeighborElements(version).OfType<UtilityGraphTerminalToTerminalConnectivityLink>().First(n => n.NeighborElements(version).Exists(n => n.Id == lisaTray11Port1TerminalId)) as UtilityGraphTerminalToTerminalConnectivityLink;


            // Check disconnect view
            var getDisconnectView = new GetDisconnectSpanEquipmentFromTerminalView(terminalToTerminalConnection.Id, lisaTray10Port1TerminalId);

            var getDisconnectViewQueryResult = await _queryDispatcher.HandleAsync<GetDisconnectSpanEquipmentFromTerminalView, Result<DisconnectSpanEquipmentFromTerminalView>>(
                getDisconnectView
            );


            // Disconnect olt with wdm
            var disconnectCmd = new DisconnectSpanSegmentsFromTerminalsAtRouteNode(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: sutNodeId,
                disconnects: new DisconnectSpanSegmentFromTerminalOperation[]
                {
                    new DisconnectSpanSegmentFromTerminalOperation(terminalToTerminalConnection.Id, lisaTray10Port1TerminalId)
                }
            );

            var disconnectCmdResult = await _commandDispatcher.HandleAsync<DisconnectSpanSegmentsFromTerminalsAtRouteNode, Result>(disconnectCmd);

            // Assert
            disconnectCmdResult.IsSuccess.Should().BeTrue();

        }

        [Fact, Order(13)]
        public async Task DisonnectLisaTray10Pin1FromLisaTray12Pin1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CO_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get lisa tray 10
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[0].SubrackMounts.First(s => s.Position == 10).TerminalEquipmentId, out var lisaTray10);
            var lisaTray10Port1TerminalId = lisaTray10.TerminalStructures.First().Terminals.First().Id;

            // Get lisa tray 12
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[0].SubrackMounts.First(s => s.Position == 12).TerminalEquipmentId, out var lisaTray12);
            var lisaTray12Port1TerminalId = lisaTray12.TerminalStructures.First().Terminals.First().Id;

            // Get terminal to terminal connection
            var version = utilityNetwork.Graph.LatestCommitedVersion;
            utilityNetwork.Graph.TryGetGraphElement<UtilityGraphConnectedTerminal>(lisaTray10Port1TerminalId, out var lisaTray10Port1Terminal);
            var terminalToTerminalConnection = lisaTray10Port1Terminal.NeighborElements(version).OfType<UtilityGraphTerminalToTerminalConnectivityLink>().First(n => n.NeighborElements(version).Exists(n => n.Id == lisaTray12Port1TerminalId)) as UtilityGraphTerminalToTerminalConnectivityLink;


            // Check disconnect view
            var getDisconnectView = new GetDisconnectSpanEquipmentFromTerminalView(terminalToTerminalConnection.Id, lisaTray10Port1TerminalId);

            var getDisconnectViewQueryResult = await _queryDispatcher.HandleAsync<GetDisconnectSpanEquipmentFromTerminalView, Result<DisconnectSpanEquipmentFromTerminalView>>(
                getDisconnectView
            );


            // Disconnect olt with wdm
            var disconnectCmd = new DisconnectSpanSegmentsFromTerminalsAtRouteNode(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: sutNodeId,
                disconnects: new DisconnectSpanSegmentFromTerminalOperation[]
                {
                    new DisconnectSpanSegmentFromTerminalOperation(terminalToTerminalConnection.Id, lisaTray10Port1TerminalId)
                }
            );

            var disconnectCmdResult = await _commandDispatcher.HandleAsync<DisconnectSpanSegmentsFromTerminalsAtRouteNode, Result>(disconnectCmd);

            // Assert
            disconnectCmdResult.IsSuccess.Should().BeTrue();

        }




        #endregion

        #region CC-1 Connectivity

        [Fact, Order(10)]
        public async Task ConnectFirstTerminalEquipmentInCC1WithFiberCable_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CC_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CC_1;
            var sutCableName = "K69373563";


            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get equipment
            var terminalEquipment = utilityNetwork.TerminalEquipmentByEquipmentId.Values.First(e => e.Name == "CC1 Splice Closure 1");

            // Get cable
            var connectivityQuery = new GetConnectivityFaces(nodeContainer.RouteNodeId);

            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetConnectivityFaces, Result<List<ConnectivityFace>>>(
                connectivityQuery
            );

            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;

            var cableId = viewModel.First(m => m.EquipmentName.StartsWith(sutCableName)).EquipmentId;

            utilityNetwork.TryGetEquipment<SpanEquipment>(cableId, out var spanEquipment);


            // ACT (do the connect between cable and equipment)
            var connectCmd = new ConnectSpanSegmentsWithTerminalsAtRouteNode(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: sutNodeId,
                connects: new ConnectSpanSegmentToTerminalOperation[]
                {
                    // Fiber 2 -> Pin 1
                    new ConnectSpanSegmentToTerminalOperation(spanEquipment.SpanStructures[2].SpanSegments[0].Id, terminalEquipment.TerminalStructures[0].Terminals[0].Id),

                    // Fiber 3 -> Pin 6
                    new ConnectSpanSegmentToTerminalOperation(spanEquipment.SpanStructures[3].SpanSegments[0].Id, terminalEquipment.TerminalStructures[0].Terminals[5].Id),
   
                    // Fiber 12 -> Pin 12
                    new ConnectSpanSegmentToTerminalOperation(spanEquipment.SpanStructures[12].SpanSegments[0].Id, terminalEquipment.TerminalStructures[0].Terminals[11].Id)

                }
            );
            var connectCmdResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsWithTerminalsAtRouteNode, Result>(connectCmd);

            // Assert
            connectCmdResult.IsSuccess.Should().BeTrue();

            utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphTerminalRef>(terminalEquipment.TerminalStructures[0].Terminals[0].Id, out var terminalRef);


            // Trace fiber 1 (should not be connected to anything)
            var fiber1TraceResult = utilityNetwork.Graph.SimpleTrace(spanEquipment.SpanStructures[1].SpanSegments[0].Id);

            fiber1TraceResult.Upstream.Length.Should().Be(0);
            fiber1TraceResult.Downstream.Length.Should().Be(0);

            // Trace fiber 2
            var fiber2TraceResult = utilityNetwork.Graph.SimpleTrace(spanEquipment.SpanStructures[2].SpanSegments[0].Id);

            var upstreamTerminalFromTrace = fiber2TraceResult.Upstream.First(t => t.Id == terminalEquipment.TerminalStructures[0].Terminals[0].Id) as IUtilityGraphTerminalRef;

            var equipmentFromTracedTerminal = upstreamTerminalFromTrace.TerminalEquipment(utilityNetwork);

            equipmentFromTracedTerminal.Should().Be(terminalEquipment);

            // Trace terminal 4
            var term4TraceResult = utilityNetwork.Graph.SimpleTrace(terminalEquipment.TerminalStructures[0].Terminals[5].Id);

            term4TraceResult.Downstream.Length.Should().Be(0);
            term4TraceResult.Upstream.Length.Should().Be(2); // a segment and a terminal at the end
            ((UtilityGraphConnectedTerminal)term4TraceResult.Upstream.Last()).RouteNodeId.Should().NotBeEmpty();


            // Check equipment connectivity view
            var connectivityViewQuery = new GetTerminalEquipmentConnectivityView(sutNodeId, terminalEquipment.Id);

            var connectivityViewResult = await _queryDispatcher.HandleAsync<GetTerminalEquipmentConnectivityView, Result<TerminalEquipmentAZConnectivityViewModel>>(
                connectivityViewQuery
            );

            connectivityViewResult.IsSuccess.Should().BeTrue();

            var connectivityTraceView = connectivityViewResult.Value;

            var teInfoToAssert = connectivityTraceView.TerminalEquipments.First(t => t.Id == terminalEquipment.Id);

            teInfoToAssert.TerminalStructures[0].Lines[0].A.Should().NotBeNull();
            teInfoToAssert.TerminalStructures[0].Lines[0].A.ConnectedTo.Should().NotBeNull();
            teInfoToAssert.TerminalStructures[0].Lines[0].A.ConnectedTo.Should().Be($"{sutCableName} (72) Tube 1 Fiber 2");

            // Check faces and face connections
            var connectivityFaceQuery = new GetConnectivityFaces(sutNodeId);

            var connectivityFaceQueryResult = await _queryDispatcher.HandleAsync<GetConnectivityFaces, Result<List<ConnectivityFace>>>(
                connectivityFaceQuery
            );

            var spanEquipmentFace = connectivityFaceQueryResult.Value.First(f => f.EquipmentName.StartsWith("K69373563"));

            // Get face connections for span equipment in CC_1 (where it is spliced)
            var spanEquipmentConnectionsQueryInCC1 = new GetConnectivityFaceConnections(sutNodeId, spanEquipmentFace.EquipmentId, spanEquipmentFace.FaceKind);

            var spanEquipmentConnectionsQueryInCC1Result = await _queryDispatcher.HandleAsync<GetConnectivityFaceConnections, Result<List<ConnectivityFaceConnection>>>(
                spanEquipmentConnectionsQueryInCC1
            );

            spanEquipmentConnectionsQueryInCC1Result.IsSuccess.Should().BeTrue();

            var spanEquipmentConnectionsInCC1 = spanEquipmentConnectionsQueryInCC1Result.Value;

            spanEquipmentConnectionsInCC1[0].IsConnected.Should().BeFalse();
            spanEquipmentConnectionsInCC1[1].IsConnected.Should().BeTrue();
            spanEquipmentConnectionsInCC1[2].IsConnected.Should().BeTrue();
            spanEquipmentConnectionsInCC1[3].IsConnected.Should().BeFalse();

            // Check face connections for span equipment in CO_1
            var spanEquipmentConnectionsQueryInCO1 = new GetConnectivityFaceConnections(TestRouteNetwork.CO_1, spanEquipmentFace.EquipmentId, spanEquipmentFace.FaceKind);

            var spanEquipmentConnectionsQueryInCO1Result = await _queryDispatcher.HandleAsync<GetConnectivityFaceConnections, Result<List<ConnectivityFaceConnection>>>(
                spanEquipmentConnectionsQueryInCO1
            );

            spanEquipmentConnectionsQueryInCO1Result.IsSuccess.Should().BeTrue();

            var spanEquipmentConnectionsInCO1 = spanEquipmentConnectionsQueryInCO1Result.Value;

            spanEquipmentConnectionsInCO1[0].IsConnected.Should().BeFalse();
            spanEquipmentConnectionsInCO1[1].IsConnected.Should().BeTrue();
            spanEquipmentConnectionsInCO1[2].IsConnected.Should().BeTrue();
            spanEquipmentConnectionsInCO1[3].IsConnected.Should().BeTrue();
        }

        [Fact, Order(11)]
        public async Task ConnectFirstTerminalEquipmentInCC1WithCustomerFiberCable_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CC_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CC_1;
            var sutCableName = "K12345678";


            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get equipment
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.TerminalEquipmentReferences.First(), out var terminalEquipment);

            // Get cable
            var connectivityQuery = new GetConnectivityFaces(nodeContainer.RouteNodeId);

            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetConnectivityFaces, Result<List<ConnectivityFace>>>(
                connectivityQuery
            );

            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;

            var cableId = viewModel.First(m => m.EquipmentName.StartsWith(sutCableName)).EquipmentId;

            utilityNetwork.TryGetEquipment<SpanEquipment>(cableId, out var spanEquipment);


            // ACT (do the connect between cable and equipment)
            var connectCmd = new ConnectSpanSegmentsWithTerminalsAtRouteNode(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: sutNodeId,
                connects: new ConnectSpanSegmentToTerminalOperation[]
                {
                    // Fiber 1 -> Tray 1 Pin 12
                    new ConnectSpanSegmentToTerminalOperation(spanEquipment.SpanStructures[1].SpanSegments[0].Id, terminalEquipment.TerminalStructures[0].Terminals[11].Id),
                }
            );
            var connectCmdResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsWithTerminalsAtRouteNode, Result>(connectCmd);

            // Assert
            connectCmdResult.IsSuccess.Should().BeTrue();
        }

        #endregion

        #region SDU-1 Connectivity

        [Fact, Order(20)]
        public async Task ConnectFirstTerminalEquipmentInSDU1WithCustomerFiberCable_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.SDU_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_SDU_1;
            var sutCableName = "K12345678";


            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get equipment
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.TerminalEquipmentReferences.First(), out var terminalEquipment);

            // Get cable
            var connectivityQuery = new GetConnectivityFaces(nodeContainer.RouteNodeId);

            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetConnectivityFaces, Result<List<ConnectivityFace>>>(
                connectivityQuery
            );

            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;

            var cableId = viewModel.First(m => m.EquipmentName.StartsWith(sutCableName)).EquipmentId;

            utilityNetwork.TryGetEquipment<SpanEquipment>(cableId, out var spanEquipment);


            // ACT (do the connect between cable and equipment)
            var connectCmd = new ConnectSpanSegmentsWithTerminalsAtRouteNode(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: sutNodeId,
                connects: new ConnectSpanSegmentToTerminalOperation[]
                {
                    // Fiber 1 -> Tray 1 Pin 1
                    new ConnectSpanSegmentToTerminalOperation(spanEquipment.SpanStructures[1].SpanSegments[0].Id, terminalEquipment.TerminalStructures[0].Terminals[0].Id),
                }
            );
            var connectCmdResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsWithTerminalsAtRouteNode, Result>(connectCmd);

            // Assert
            connectCmdResult.IsSuccess.Should().BeTrue();
        }

        #endregion



        [Fact, Order(1000)]
        public async Task TestTrace_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CO_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;
            var sutCableName = "K69373563";
        
            var cable = FindSpanEquipmentRelatedToRouteNetworkElementByName(sutNodeId, sutCableName);

            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new EquipmentIdList() { cable.Id })
                {
                    EquipmentDetailsFilter = new EquipmentDetailsFilterOptions()
                    {
                        IncludeRouteNetworkTrace = true
                    }
                }
            );

            // Assert
            equipmentQueryResult.IsSuccess.Should().BeTrue();

            //equipmentQueryResult.Value.RouteNetworkTraces.Should().NotBeNull();

        }



        [Fact, Order(1001)]
        public async Task CheckThatLISAInJ1Has24PatchesAnd24SplicesInTray_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.J_1;

            var connectivityQuery = new GetConnectivityFaces(sutNodeId);

            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetConnectivityFaces, Result<List<ConnectivityFace>>>(
                connectivityQuery
            );

            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;

            var face = viewModel.First(f => f.EquipmentName.StartsWith("LISA APC"));


            // Check equipment connectivity view
            var connectivityViewQuery = new GetTerminalEquipmentConnectivityView(sutNodeId, face.EquipmentId);

            var connectivityViewResult = await _queryDispatcher.HandleAsync<GetTerminalEquipmentConnectivityView, Result<TerminalEquipmentAZConnectivityViewModel>>(
                connectivityViewQuery
            );

            connectivityViewResult.IsSuccess.Should().BeTrue();

            var connectivityTraceView = connectivityViewResult.Value;

            connectivityTraceView.TerminalEquipments.First().TerminalStructures.First().Lines.Count(l => (l.A != null && l.A.FaceKind == FaceKindEnum.SpliceSide) || (l.Z != null && l.Z.FaceKind == FaceKindEnum.SpliceSide)).Should().Be(24);
            connectivityTraceView.TerminalEquipments.First().TerminalStructures.First().Lines.Count(l => (l.A != null && l.A.FaceKind == FaceKindEnum.PatchSide) || (l.Z != null && l.Z.FaceKind == FaceKindEnum.PatchSide)).Should().Be(24);
        }



        [Fact, Order(1002)]
        public async Task CheckThatBUDIInCC1Has0Patches12SplicesInTray_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CC_1;

            var connectivityQuery = new GetConnectivityFaces(sutNodeId);

            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetConnectivityFaces, Result<List<ConnectivityFace>>>(
                connectivityQuery
            );

            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;

            var face = viewModel.First(f => f.EquipmentName.StartsWith("BUDI"));


            // Check equipment connectivity view
            var connectivityViewQuery = new GetTerminalEquipmentConnectivityView(sutNodeId, face.EquipmentId);

            var connectivityViewResult = await _queryDispatcher.HandleAsync<GetTerminalEquipmentConnectivityView, Result<TerminalEquipmentAZConnectivityViewModel>>(
                connectivityViewQuery
            );

            connectivityViewResult.IsSuccess.Should().BeTrue();

            var connectivityTraceView = connectivityViewResult.Value;

            connectivityTraceView.TerminalEquipments.First().TerminalStructures.First().Lines.Count(l => (l.A != null && l.A.FaceKind == FaceKindEnum.SpliceSide) || (l.Z != null && l.Z.FaceKind == FaceKindEnum.SpliceSide)).Should().Be(12);
            connectivityTraceView.TerminalEquipments.First().TerminalStructures.First().Lines.Count(l => (l.A != null && l.A.FaceKind == FaceKindEnum.PatchSide) || (l.Z != null && l.Z.FaceKind == FaceKindEnum.PatchSide)).Should().Be(0);
        }

        [Fact, Order(2000)]
        public async Task ConnecLisaTray80InODFRackInCO1WithFiberCable_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CO_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;
            var sutCableName = "K69373563";

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get lisa tray
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[0].SubrackMounts.First(s => s.Position == 79).TerminalEquipmentId, out var lisaTray80);

            // Get cable
            var connectivityQuery = new GetConnectivityFaces(nodeContainer.RouteNodeId);

            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetConnectivityFaces, Result<List<ConnectivityFace>>>(
                connectivityQuery
            );

            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;

            var cableId = viewModel.First(m => m.EquipmentName.StartsWith(sutCableName)).EquipmentId;

            utilityNetwork.TryGetEquipment<SpanEquipment>(cableId, out var cableSpanEquipment);


            // ACT (do the connect between cable and equipment)
            var connectCmd = new ConnectSpanSegmentsWithTerminalsAtRouteNode(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: sutNodeId,
                connects: new ConnectSpanSegmentToTerminalOperation[]
                {
                    // Fiber 12 -> lisa Tray 80 Pin 1
                    new ConnectSpanSegmentToTerminalOperation(cableSpanEquipment.SpanStructures[13].SpanSegments[0].Id, lisaTray80.TerminalStructures[0].Terminals[0].Id)
                }
            );
            var connectCmdResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsWithTerminalsAtRouteNode, Result>(connectCmd);

            // Assert
            connectCmdResult.IsSuccess.Should().BeTrue();

        }



        private SpanEquipment? FindSpanEquipmentRelatedToRouteNetworkElementByName(Guid routeNetworkElementId, string spanEquipmentName)
        {
            var routeNetworkQueryResult = _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
              new GetRouteNetworkDetails(new RouteNetworkElementIdList() { routeNetworkElementId })
              {
                  RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
              }
            ).Result;

            InterestIdList interestIdList = new InterestIdList();
            foreach (var interestRel in routeNetworkQueryResult.Value.RouteNetworkElements[routeNetworkElementId].InterestRelations)
            {
                interestIdList.Add(interestRel.RefId);
            }

            var equipmentQueryResult = _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                    new GetEquipmentDetails(interestIdList)
                    {
                        EquipmentDetailsFilter = new EquipmentDetailsFilterOptions() { IncludeRouteNetworkTrace = true }
                    }
                ).Result;

            foreach (var spanEquipment in equipmentQueryResult.Value.SpanEquipment)
            {
                if (spanEquipment.Name == spanEquipmentName)
                    return spanEquipment;
            }

            return null;
        }
    }
}
