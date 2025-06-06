﻿using AwesomeAssertions;
using OpenFTTH.Results;
using Microsoft.Extensions.Logging;
using Moq;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Schematic.API.Model.DiagramLayout;
using OpenFTTH.Schematic.API.Queries;
using OpenFTTH.Schematic.Business.IO;
using OpenFTTH.Schematic.Business.QueryHandler;
using OpenFTTH.Schematic.Business.SchematicBuilder;
using OpenFTTH.TestData;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.Business.Graph;
using System;
using System.Linq;
using Xunit;
using Xunit.Extensions.Ordering;

namespace OpenFTTH.Schematic.Tests.NodeSchematic
{
    [Order(2)]
    public class NodeSchematicTests
    {
        private readonly IEventStore _eventStore;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;

        private static TestUtilityNetwork _utilityNetwork;

        public NodeSchematicTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;

            new TestSpecifications(commandDispatcher, queryDispatcher).Run();
            _utilityNetwork = new TestUtilityNetwork(commandDispatcher, queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public void TestDrawingSingleDetachedMultiConduit_5x10_HH_1_to_HH_10()
        {
            var logger = Mock.Of<ILogger<GetDiagramQueryHandler>>();

            var sutRouteNode = TestRouteNetwork.CC_1;

            var data = RouteNetworkElementRelatedData.FetchData(_queryDispatcher, sutRouteNode).Value;

            var spanEquipment = data.SpanEquipments[TestUtilityNetwork.MultiConduit_12x7_HH_1_to_HH_10];

            // Create read model
            var readModel = new SpanEquipmentViewModel(logger, sutRouteNode, spanEquipment.Id, data);

            var builder = new DetachedSpanEquipmentBuilder(logger, readModel);

            // Create the diagram
            Diagram diagram = new Diagram();

            builder.CreateDiagramObjects(diagram, 0, 0);

            // Assert
            diagram.DiagramObjects.Count(o => o.Style == "OuterConduitOrange").Should().Be(1);
            diagram.DiagramObjects.Count(o => o.Style.Contains("InnerConduit")).Should().Be(12);
            diagram.DiagramObjects.Count(o => o.Style == "WestTerminalLabel").Should().Be(12);
            diagram.DiagramObjects.Count(o => o.Label == "HH-1").Should().Be(12);
            diagram.DiagramObjects.Count(o => o.Style == "EastTerminalLabel").Should().Be(12);
            diagram.DiagramObjects.Count(o => o.Label == "HH-10").Should().Be(12);
            diagram.DiagramObjects.Count(o => o.IdentifiedObject != null && o.IdentifiedObject.RefClass == "SpanSegment").Should().Be(37);

            if (System.Environment.OSVersion.Platform.ToString() == "Win32NT")
                new GeoJsonExporter(diagram).Export("c:/temp/diagram/test.geojson");
        }

        [Fact, Order(2)]
        public async void TestAffixConduitInCC_1()
        {
            // Affix 5x10 to west side
            var conduit1Id = TestUtilityNetwork.MultiConduit_12x7_HH_1_to_HH_10;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
            utilityNetwork.TryGetEquipment<SpanEquipment>(conduit1Id, out var conduit1);

            var conduit1AffixCommand = new AffixSpanEquipmentToNodeContainer(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                spanEquipmentOrSegmentId: conduit1.SpanStructures[0].SpanSegments[0].Id,
                nodeContainerId: TestUtilityNetwork.NodeContainer_CC_1,
                nodeContainerIngoingSide: NodeContainerSideEnum.West
            );

            var conduit1AffixResult = await _commandDispatcher.HandleAsync<AffixSpanEquipmentToNodeContainer, Result>(conduit1AffixCommand);

            // Affix 3x10 to north side
            var conduit2Id = TestUtilityNetwork.MultiConduit_3x10_CC_1_to_SP_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(conduit2Id, out var conduit2);

            var conduit2AffixCommand = new AffixSpanEquipmentToNodeContainer(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                spanEquipmentOrSegmentId: conduit2.SpanStructures[0].SpanSegments[0].Id,
                nodeContainerId: TestUtilityNetwork.NodeContainer_CC_1,
                nodeContainerIngoingSide: NodeContainerSideEnum.North
            );

            var conduit2AffixResult = await _commandDispatcher.HandleAsync<AffixSpanEquipmentToNodeContainer, Result>(conduit2AffixCommand);

            // Affix flex conduit to south side
            var conduit3Id = TestUtilityNetwork.FlexConduit_40_Red_CC_1_to_SP_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(conduit3Id, out var conduit3);

            var conduit3AffixCommand = new AffixSpanEquipmentToNodeContainer(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                spanEquipmentOrSegmentId: conduit3.SpanStructures[0].SpanSegments[0].Id,
                nodeContainerId: TestUtilityNetwork.NodeContainer_CC_1,
                nodeContainerIngoingSide: NodeContainerSideEnum.South
            );

            var conduit3AffixResult = await _commandDispatcher.HandleAsync<AffixSpanEquipmentToNodeContainer, Result>(conduit3AffixCommand);

            // Act
            var getDiagramQueryResult = await _queryDispatcher.HandleAsync<GetDiagram, Result<GetDiagramResult>>(new GetDiagram(TestRouteNetwork.CC_1));

            if (System.Environment.OSVersion.Platform.ToString() == "Win32NT")
                new GeoJsonExporter(getDiagramQueryResult.Value.Diagram).Export("c:/temp/diagram/test.geojson");

            // Assert
            getDiagramQueryResult.IsSuccess.Should().BeTrue();
            var diagram = getDiagramQueryResult.Value.Diagram;

            diagram.DiagramObjects.Count(o => o.Style == "NodeContainer").Should().Be(1);
            diagram.DiagramObjects.Count(o => o.Style == "NodeContainerSideWest").Should().Be(1);

            diagram.DiagramObjects.Count(o => o.Style == "WestTerminalLabel" && o.IdentifiedObject.RefId == conduit1.SpanStructures[1].SpanSegments[0].Id).Should().Be(1);

            diagram.DiagramObjects.Count(o => o.Style == "SouthTerminalLabel" && o.IdentifiedObject.RefId == conduit3.SpanStructures[0].SpanSegments[0].Id).Should().Be(2);

            // Only one terminal connection should be shown in conduit 1 that is affixed to the node container
            diagram.DiagramObjects.Count(o => o.Style == "OuterConduitOrange" && o.IdentifiedObject.RefId == conduit1.SpanStructures[0].SpanSegments[0].Id).Should().Be(1);

            diagram.DiagramObjects.Any(d => d.DrawingOrder == 0).Should().BeFalse();
        }

        [Fact, Order(3)]
        public async void CutPassThroughConduitInCC1()
        {
            var sutRouteNetworkElement = TestRouteNetwork.CC_1;
            var sutSpanEquipment = TestUtilityNetwork.MultiConduit_12x7_HH_1_to_HH_10;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            // Act
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipment, out var spanEquipment);

            // Cut segments in structure 1 (the outer conduit and second inner conduit)
            var cutCmd = new CutSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CC_1,
                spanSegmentsToCut: new Guid[] {
                    spanEquipment.SpanStructures[0].SpanSegments[0].Id,
                    spanEquipment.SpanStructures[2].SpanSegments[0].Id,
                    spanEquipment.SpanStructures[4].SpanSegments[0].Id,
                    spanEquipment.SpanStructures[6].SpanSegments[0].Id,
                    spanEquipment.SpanStructures[7].SpanSegments[0].Id
                }
            );

            var cutResult = await _commandDispatcher.HandleAsync<CutSpanSegmentsAtRouteNode, Result>(cutCmd);

            cutResult.IsSuccess.Should().BeTrue();

            var getDiagramQueryResult = await _queryDispatcher.HandleAsync<GetDiagram, Result<GetDiagramResult>>(new GetDiagram(TestRouteNetwork.CC_1));

            if (System.Environment.OSVersion.Platform.ToString() == "Win32NT")
                new GeoJsonExporter(getDiagramQueryResult.Value.Diagram).Export("c:/temp/diagram/test.geojson");


        }

        [Fact, Order(4)]
        public async void CutSouthAndEastSideConduitInCC1()
        {
            var sutRouteNetworkElement = TestRouteNetwork.CC_1;
            var sutSpanEquipmentFrom = TestUtilityNetwork.MultiConduit_12x7_HH_1_to_HH_10;
            var sutSpanEquipmentTo = TestUtilityNetwork.MultiConduit_3x10_CC_1_to_SP_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            // Act
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentFrom, out var fromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentTo, out var toSpanEquipment);

            // Connect segments
            var connectCmd = new ConnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CC_1,
                spanSegmentsToConnect: new Guid[] {
                    toSpanEquipment.SpanStructures[1].SpanSegments[0].Id,
                    fromSpanEquipment.SpanStructures[2].SpanSegments[1].Id,
                }
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd);

            connectResult.IsSuccess.Should().BeTrue();

            var getDiagramQueryResult = await _queryDispatcher.HandleAsync<GetDiagram, Result<GetDiagramResult>>(new GetDiagram(TestRouteNetwork.CC_1));

            if (System.Environment.OSVersion.Platform.ToString() == "Win32NT")
                new GeoJsonExporter(getDiagramQueryResult.Value.Diagram).Export("c:/temp/diagram/test.geojson");


        }





        [Fact, Order(10)]
        public async void TestAddAdditionalInnerConduitsToPassThroughConduitInJ_1()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipmentId = TestUtilityNetwork.FlexConduit_40_Red_SDU_1_to_SDU_2;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var sutSpanEquipment);

            // Add two inner conduits
            var addStructure = new PlaceAdditionalStructuresInSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                spanEquipmentId: sutSpanEquipmentId,
                structureSpecificationIds: new Guid[] { TestSpecifications.Ø10_Red, TestSpecifications.Ø10_Violet }
            );

            var addStructureResult = await _commandDispatcher.HandleAsync<PlaceAdditionalStructuresInSpanEquipment, Result>(addStructure);

            // Add two more inner conduits
            var addStructure2 = new PlaceAdditionalStructuresInSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                spanEquipmentId: sutSpanEquipmentId,
                structureSpecificationIds: new Guid[] { TestSpecifications.Ø10_Brown, TestSpecifications.Ø10_Brown }
            );

            var addStructureResult2 = await _commandDispatcher.HandleAsync<PlaceAdditionalStructuresInSpanEquipment, Result>(addStructure2);


            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
               new GetEquipmentDetails(new EquipmentIdList() { sutSpanEquipmentId })
            );

            var equipmentAfterAddingStructure = equipmentQueryResult.Value.SpanEquipment[sutSpanEquipmentId];

            // Act
            var getDiagramQueryResult = await _queryDispatcher.HandleAsync<GetDiagram, Result<GetDiagramResult>>(new GetDiagram(TestRouteNetwork.J_1));

            if (System.Environment.OSVersion.Platform.ToString() == "Win32NT")
                new GeoJsonExporter(getDiagramQueryResult.Value.Diagram).Export("c:/temp/diagram/test.geojson");

            // Assert
            getDiagramQueryResult.IsSuccess.Should().BeTrue();
            var diagram = getDiagramQueryResult.Value.Diagram;

            addStructureResult.IsSuccess.Should().BeTrue();
            addStructureResult2.IsSuccess.Should().BeTrue();
            getDiagramQueryResult.IsSuccess.Should().BeTrue();

            // Assert that no empty guids
            diagram.DiagramObjects.Any(d => d.IdentifiedObject != null && d.IdentifiedObject.RefId == Guid.Empty).Should().BeFalse();

            // Check that all added inner conduits are there
            diagram.DiagramObjects.Any(d => d.IdentifiedObject != null && d.IdentifiedObject.RefId == equipmentAfterAddingStructure.SpanStructures[1].SpanSegments[0].Id).Should().BeTrue();
            diagram.DiagramObjects.Any(d => d.IdentifiedObject != null && d.IdentifiedObject.RefId == equipmentAfterAddingStructure.SpanStructures[2].SpanSegments[0].Id).Should().BeTrue();
            diagram.DiagramObjects.Any(d => d.IdentifiedObject != null && d.IdentifiedObject.RefId == equipmentAfterAddingStructure.SpanStructures[3].SpanSegments[0].Id).Should().BeTrue();
            diagram.DiagramObjects.Any(d => d.IdentifiedObject != null && d.IdentifiedObject.RefId == equipmentAfterAddingStructure.SpanStructures[4].SpanSegments[0].Id).Should().BeTrue();

        }


        [Fact, Order(11)]
        public async void TestThatRemovedStructuresAreNotShownInDiagram()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipmentId = TestUtilityNetwork.FlexConduit_40_Red_SDU_1_to_SDU_2;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var sutSpanEquipment);

            // Remove inner conduit 1 from flexconduit
            var removeStructureCmd = new RemoveSpanStructureFromSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), sutSpanEquipment.SpanStructures[1].SpanSegments[0].Id);

            var removeStructureCmdResult = await _commandDispatcher.HandleAsync<RemoveSpanStructureFromSpanEquipment, Result>(removeStructureCmd);


            // Act
            var getDiagramQueryResult = await _queryDispatcher.HandleAsync<GetDiagram, Result<GetDiagramResult>>(new GetDiagram(TestRouteNetwork.J_1));

            if (System.Environment.OSVersion.Platform.ToString() == "Win32NT")
                new GeoJsonExporter(getDiagramQueryResult.Value.Diagram).Export("c:/temp/diagram/test.geojson");

            // Assert
            removeStructureCmdResult.IsSuccess.Should().BeTrue();
            getDiagramQueryResult.IsSuccess.Should().BeTrue();
            var diagram = getDiagramQueryResult.Value.Diagram;

            // Make sure the fist inner conduit is gone
            diagram.DiagramObjects.Any(d => d.IdentifiedObject != null && d.IdentifiedObject.RefId == sutSpanEquipment.SpanStructures[1].SpanSegments[0].Id).Should().BeFalse();

        }

        [Fact, Order(12)]
        public async void TestThatRemovedSpanEquipmentAreNotShownInDiagram()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipmentId = TestUtilityNetwork.MultiConduit_5x10_SDU_1_to_SDU_2;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var sutSpanEquipment);

            // Remove outer conduit (which means remove the whole thing)
            var removeStructureCmd = new RemoveSpanStructureFromSpanEquipment(Guid.NewGuid(), new UserContext("test", Guid.Empty), sutSpanEquipment.SpanStructures[0].SpanSegments[0].Id);
            var removeStructureCmdResult = await _commandDispatcher.HandleAsync<RemoveSpanStructureFromSpanEquipment, Result>(removeStructureCmd);

            // Act
            var getDiagramQueryResult = await _queryDispatcher.HandleAsync<GetDiagram, Result<GetDiagramResult>>(new GetDiagram(TestRouteNetwork.J_1));

            if (System.Environment.OSVersion.Platform.ToString() == "Win32NT")
                new GeoJsonExporter(getDiagramQueryResult.Value.Diagram).Export("c:/temp/diagram/test.geojson");

            // Assert
            removeStructureCmdResult.IsSuccess.Should().BeTrue();
            getDiagramQueryResult.IsSuccess.Should().BeTrue();
            var diagram = getDiagramQueryResult.Value.Diagram;

            // Make sure the no inner conduit is gone
            diagram.DiagramObjects.Any(d => d.IdentifiedObject != null && d.IdentifiedObject.RefId == sutSpanEquipment.SpanStructures[0].SpanSegments[0].Id).Should().BeFalse();
            diagram.DiagramObjects.Any(d => d.IdentifiedObject != null && d.IdentifiedObject.RefId == sutSpanEquipment.SpanStructures[1].SpanSegments[0].Id).Should().BeFalse();
            diagram.DiagramObjects.Any(d => d.IdentifiedObject != null && d.IdentifiedObject.RefId == sutSpanEquipment.SpanStructures[2].SpanSegments[0].Id).Should().BeFalse();
            diagram.DiagramObjects.Any(d => d.IdentifiedObject != null && d.IdentifiedObject.RefId == sutSpanEquipment.SpanStructures[3].SpanSegments[0].Id).Should().BeFalse();
            diagram.DiagramObjects.Any(d => d.IdentifiedObject != null && d.IdentifiedObject.RefId == sutSpanEquipment.SpanStructures[4].SpanSegments[0].Id).Should().BeFalse();
            diagram.DiagramObjects.Any(d => d.IdentifiedObject != null && d.IdentifiedObject.RefId == sutSpanEquipment.SpanStructures[5].SpanSegments[0].Id).Should().BeFalse();

        }

        [Fact, Order(13)]
        public async void TestVerticalAlignmentDiagram()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutSpanEquipmentId = TestUtilityNetwork.MultiConduit_5x10_SDU_1_to_SDU_2;

            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentId, out var sutSpanEquipment);

            // Act
            var getDiagramQueryBeforeReverseResult = await _queryDispatcher.HandleAsync<GetDiagram, Result<GetDiagramResult>>(new GetDiagram(TestRouteNetwork.J_1));

            if (System.Environment.OSVersion.Platform.ToString() == "Win32NT")
                new GeoJsonExporter(getDiagramQueryBeforeReverseResult.Value.Diagram).Export("c:/temp/diagram/test.geojson");


            var reverseAlignmentCmd = new ReverseNodeContainerVerticalContentAlignment(Guid.NewGuid(), new UserContext("test", Guid.Empty), TestUtilityNetwork.NodeContainer_J_1);
            var reverseAlignmentCmdResult = await _commandDispatcher.HandleAsync<ReverseNodeContainerVerticalContentAlignment, Result>(reverseAlignmentCmd);

            var getDiagramQueryAfterReverseResult = await _queryDispatcher.HandleAsync<GetDiagram, Result<GetDiagramResult>>(new GetDiagram(TestRouteNetwork.J_1));

            if (System.Environment.OSVersion.Platform.ToString() == "Win32NT")
                new GeoJsonExporter(getDiagramQueryAfterReverseResult.Value.Diagram).Export("c:/temp/diagram/test.geojson");

            // Assert
            var conduit40BeforeMaxY = getDiagramQueryBeforeReverseResult.Value.Diagram.DiagramObjects.Find(d => d.Style == "WestTerminalLabel" && d.Label == "Ø40 5x10").Geometry.EnvelopeInternal.MaxY;
            var conduit32BeforeMaxY = getDiagramQueryBeforeReverseResult.Value.Diagram.DiagramObjects.Find(d => d.Style == "WestTerminalLabel" && d.Label == "Ø32 3x10").Geometry.EnvelopeInternal.MaxY;
            conduit40BeforeMaxY.Should().BeGreaterThan(conduit32BeforeMaxY);

            var conduit40AfterMaxY = getDiagramQueryAfterReverseResult.Value.Diagram.DiagramObjects.Find(d => d.Style == "WestTerminalLabel" && d.Label == "Ø40 5x10").Geometry.EnvelopeInternal.MaxY;
            var conduit32AfterMaxY = getDiagramQueryAfterReverseResult.Value.Diagram.DiagramObjects.Find(d => d.Style == "WestTerminalLabel" && d.Label == "Ø32 3x10").Geometry.EnvelopeInternal.MaxY;
            conduit40AfterMaxY.Should().BeLessThan(conduit32AfterMaxY);
        }


        [Fact, Order(20)]
        public async void TestAffixConduitInHH_10()
        {
            // Affix 5x10 to west side
            var conduit1Id = TestUtilityNetwork.MultiConduit_12x7_HH_1_to_HH_10;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
            utilityNetwork.TryGetEquipment<SpanEquipment>(conduit1Id, out var conduit1);

            var conduit1AffixCommand = new AffixSpanEquipmentToNodeContainer(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                spanEquipmentOrSegmentId: conduit1.SpanStructures[0].SpanSegments[0].Id,
                nodeContainerId: TestUtilityNetwork.NodeContainer_HH_10,
                nodeContainerIngoingSide: NodeContainerSideEnum.West
            );

            var conduit1AffixResult = await _commandDispatcher.HandleAsync<AffixSpanEquipmentToNodeContainer, Result>(conduit1AffixCommand);

            // Act
            var getDiagramQueryResult = await _queryDispatcher.HandleAsync<GetDiagram, Result<GetDiagramResult>>(new GetDiagram(TestRouteNetwork.HH_10));

            if (System.Environment.OSVersion.Platform.ToString() == "Win32NT")
                new GeoJsonExporter(getDiagramQueryResult.Value.Diagram).Export("c:/temp/diagram/test.geojson");

            // Assert
            getDiagramQueryResult.IsSuccess.Should().BeTrue();
            var diagram = getDiagramQueryResult.Value.Diagram;

            diagram.DiagramObjects.Count(o => o.Label == "HH-1").Should().Be(18);
            diagram.DiagramObjects.Count(o => o.Label == "SP-1").Should().Be(1);
            diagram.DiagramObjects.Count(o => o.Label == "HH-10").Should().Be(1);

        }

        [Fact, Order(30)]
        public async void TestConnectSingleConduitInCC1()
        {
            var sutRouteNetworkElement = TestRouteNetwork.CC_1;
            var sutSpanEquipmentFrom = TestUtilityNetwork.MultiConduit_12x7_HH_1_to_HH_10;
            var sutSpanEquipmentTo = TestUtilityNetwork.CustomerConduit_CC_1_to_SDU_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            // Act
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentFrom, out var fromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentTo, out var toSpanEquipment);

            // Connect segments
            var connectCmd = new ConnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CC_1,
                spanSegmentsToConnect: new Guid[] {
                    fromSpanEquipment.SpanStructures[6].SpanSegments[0].Id,
                    toSpanEquipment.SpanStructures[0].SpanSegments[0].Id
                }
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd);

            connectResult.IsSuccess.Should().BeTrue();

            var getDiagramQueryResult = await _queryDispatcher.HandleAsync<GetDiagram, Result<GetDiagramResult>>(new GetDiagram(TestRouteNetwork.CC_1));

            if (System.Environment.OSVersion.Platform.ToString() == "Win32NT")
                new GeoJsonExporter(getDiagramQueryResult.Value.Diagram).Export("c:/temp/diagram/test.geojson");


        }


        [Fact, Order(31)]
        public async void TestConnectOneMoreSingleConduitInCC1()
        {
            var sutRouteNetworkElement = TestRouteNetwork.CC_1;
            var sutSpanEquipmentFrom = TestUtilityNetwork.MultiConduit_12x7_HH_1_to_HH_10;
            var sutSpanEquipmentTo = TestUtilityNetwork.CustomerConduit_CC_1_to_SDU_2;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            // Act
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentFrom, out var fromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(sutSpanEquipmentTo, out var toSpanEquipment);

            // Connect segments
            var connectCmd = new ConnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CC_1,
                spanSegmentsToConnect: new Guid[] {
                    fromSpanEquipment.SpanStructures[7].SpanSegments[0].Id,
                    toSpanEquipment.SpanStructures[0].SpanSegments[0].Id
                }
            );

            var connectResult = await _commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd);

            connectResult.IsSuccess.Should().BeTrue();

            var getDiagramQueryResult = await _queryDispatcher.HandleAsync<GetDiagram, Result<GetDiagramResult>>(new GetDiagram(TestRouteNetwork.CC_1));

            if (System.Environment.OSVersion.Platform.ToString() == "Win32NT")
                new GeoJsonExporter(getDiagramQueryResult.Value.Diagram).Export("c:/temp/diagram/test.geojson");


        }

    }
}
