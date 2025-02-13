using DAX.EventProcessing;
using FluentAssertions;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.TestData;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.NodeContainers;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions.Ordering;

#nullable disable

namespace OpenFTTH.UtilityGraphService.Tests.UtilityNetwork
{
    [Order(4900)]
    public class T4900_RackEquipmentPlacmenentTests
    {
        private readonly IEventStore _eventStore;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly FakeExternalEventProducer _externalEventProducer;

        public T4900_RackEquipmentPlacmenentTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = (FakeExternalEventProducer)externalEventProducer;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public async Task PlaceFirstRackInContainerInJ2_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_J_1;

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerBeforeCommand);

            var placeRackCmd = new PlaceRackInNodeContainer(
                Guid.NewGuid(),
                new UserContext("test", Guid.Empty),
                sutNodeContainerId,
                Guid.NewGuid(),
                TestSpecifications.Rack_ESTI,
                "Rack 1",
                80
            );

            var placeRackResult = await _commandDispatcher.HandleAsync<PlaceRackInNodeContainer, Result>(placeRackCmd);

            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new InterestIdList() { nodeContainerBeforeCommand.InterestId })
            );

            // Assert
            placeRackResult.IsSuccess.Should().BeTrue();
            equipmentQueryResult.IsSuccess.Should().BeTrue();
            var nodeContainerAfterCommand = equipmentQueryResult.Value.NodeContainers[sutNodeContainerId];

            // Check that rack was added to node container
            nodeContainerAfterCommand.Should().NotBeNull();
            nodeContainerAfterCommand.Racks.Should().NotBeNull();
            nodeContainerAfterCommand.Racks[0].Name.Should().Be(placeRackCmd.RackName);
            nodeContainerAfterCommand.Racks[0].Position.Should().Be(1);
            nodeContainerAfterCommand.Racks[0].SpecificationId.Should().Be(placeRackCmd.RackSpecificationId);

            // Check if an event is published to the notification.utility-network topic having an idlist containing the node container we just changed
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == sutNodeContainerId)));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(nodeContainerBeforeCommand.RouteNodeId);
        }

        [Fact, Order(2)]
        public async Task PlaceSecondRackInContainerInJ2_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_J_1;

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerBeforeCommand);

            var placeRackCmd = new PlaceRackInNodeContainer(
                Guid.NewGuid(),
                new UserContext("test", Guid.Empty),
                sutNodeContainerId,
                Guid.NewGuid(),
                TestSpecifications.Rack_ESTI,
                "Rack 2",
                80
            );

            var placeRackResult = await _commandDispatcher.HandleAsync<PlaceRackInNodeContainer, Result>(placeRackCmd);

            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new InterestIdList() { nodeContainerBeforeCommand.InterestId })
            );

            // Assert
            placeRackResult.IsSuccess.Should().BeTrue();
            equipmentQueryResult.IsSuccess.Should().BeTrue();
            var nodeContainerAfterCommand = equipmentQueryResult.Value.NodeContainers[sutNodeContainerId];

            // Check that rack was added to node container
            nodeContainerAfterCommand.Should().NotBeNull();
            nodeContainerAfterCommand.Racks.Should().NotBeNull();
            nodeContainerAfterCommand.Racks.Count().Should().Be(2);

            nodeContainerAfterCommand.Racks[1].Name.Should().Be(placeRackCmd.RackName);
            nodeContainerAfterCommand.Racks[1].Position.Should().Be(2);
            nodeContainerAfterCommand.Racks[1].SpecificationId.Should().Be(placeRackCmd.RackSpecificationId);

            // Check if an event is published to the notification.utility-network topic having an idlist containing the node container we just changed
            var utilityNetworkNotifications = _externalEventProducer.GetMessagesByTopic(nameof(RouteNetworkElementContainedEquipmentUpdated)).OfType<RouteNetworkElementContainedEquipmentUpdated>();
            var utilityNetworkUpdatedEvent = utilityNetworkNotifications.First(n => n.Category == "EquipmentModification" && n.IdChangeSets != null && n.IdChangeSets.Any(i => i.IdList.Any(i => i == sutNodeContainerId)));
            utilityNetworkUpdatedEvent.AffectedRouteNetworkElementIds.Should().Contain(nodeContainerBeforeCommand.RouteNodeId);
        }


        [Fact, Order(3)]
        public async Task PlaceODFRackInCO_1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerBeforeCommand);

            var placeRackCmd = new PlaceRackInNodeContainer(
                Guid.NewGuid(),
                new UserContext("test", Guid.Empty),
                sutNodeContainerId,
                Guid.NewGuid(),
                TestSpecifications.Rack_ESTI,
                "ODF",
                80
            );

            var placeRackResult = await _commandDispatcher.HandleAsync<PlaceRackInNodeContainer, Result>(placeRackCmd);

            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new InterestIdList() { nodeContainerBeforeCommand.InterestId })
            );

            // Assert
            placeRackResult.IsSuccess.Should().BeTrue();
        }

        
        [Fact, Order(4)]
        public async Task Place80LisaTraysInODFRackInCO1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerBeforeCommand);

            var placeEquipmentCmd = new PlaceTerminalEquipmentInNodeContainer(
            correlationId: Guid.NewGuid(),
            userContext: new UserContext("test", Guid.Empty),
            nodeContainerId: sutNodeContainerId,
            Guid.NewGuid(),
            terminalEquipmentSpecificationId: TestSpecifications.Subrack_LISA_APC_UPC,
            numberOfEquipments: 80,
            startSequenceNumber: 1,
            namingMethod: TerminalEquipmentNamingMethodEnum.NumberOnly,
            namingInfo: null
             )
            {
                SubrackPlacementInfo = new SubrackPlacementInfo(nodeContainerBeforeCommand.Racks[0].Id, 0, SubrackPlacmentMethod.BottomUp)
            };


            // Act
            var placeEquipmentCmdResult = await _commandDispatcher.HandleAsync<PlaceTerminalEquipmentInNodeContainer, Result>(placeEquipmentCmd);


            // Assert
            placeEquipmentCmdResult.IsSuccess.Should().BeTrue();
        }
        
        [Fact, Order(5)]
        public async Task Place_1_to_2_SplitterGSSInODFRackInCO1_ShouldSucceed()
        {
            // Setup
            var sutNodeContainer = TestUtilityNetwork.NodeContainer_CO_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainer, out var nodeContainerBeforeCommand);

            var placeEquipmentCmd = new PlaceTerminalEquipmentInNodeContainer(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                nodeContainerId: sutNodeContainer,
                Guid.NewGuid(),
                terminalEquipmentSpecificationId: TestSpecifications.GSS_24_Splitters,
                numberOfEquipments: 1,
                startSequenceNumber: 1,
                namingMethod: TerminalEquipmentNamingMethodEnum.NameOnly,
                namingInfo: new Events.Core.Infos.NamingInfo() { Name = "1:2 Split"}
            )
            {
                SubrackPlacementInfo = new SubrackPlacementInfo(nodeContainerBeforeCommand.Racks[0].Id, 100, SubrackPlacmentMethod.BottomUp)
            };


            // Act
            var placeEquipmentCmdResult = await _commandDispatcher.HandleAsync<PlaceTerminalEquipmentInNodeContainer, Result>(placeEquipmentCmd);

            var nodeContainerQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new EquipmentIdList() { placeEquipmentCmd.NodeContainerId })
            );

            var nodeContainer = nodeContainerQueryResult.Value.NodeContainers.First();


            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new EquipmentIdList(nodeContainer.Racks[0].SubrackMounts.Select(s => s.TerminalEquipmentId)))
            );

            var firstMount = nodeContainer.Racks[0].SubrackMounts[40];
            var sutEquipment = equipmentQueryResult.Value.TerminalEquipment[firstMount.TerminalEquipmentId];


            // Assert
            placeEquipmentCmdResult.IsSuccess.Should().BeTrue();
            nodeContainerQueryResult.IsSuccess.Should().BeTrue();
            equipmentQueryResult.IsSuccess.Should().BeTrue();

            // Try simple trace of splitter 1 terminal 1 (in port) - should return nothing
            var splitter1terminal1traceResult = utilityNetwork.Graph.SimpleTrace(sutEquipment.TerminalStructures.First().Terminals.First().Id);
            splitter1terminal1traceResult.Upstream.Length.Should().Be(0);
            splitter1terminal1traceResult.Downstream.Length.Should().Be(0);


            // Try advanced trace of splitter 1 terminal 1 (in port) - should return the two output terminals downstream
            var splitter1terminal1advancedTraceResult = utilityNetwork.Graph.OutageViewTrace(sutEquipment.TerminalStructures.First().Terminals.First().Id);
            //splitter1terminal1advancedTraceResult.Upstream.Length.Should().Be(0);
            //splitter1terminal1advancedTraceResult.Downstream.Length.Should().Be(4);
        }

        [Fact, Order(6)]
        public async Task PlaceEmplyLISASplitterHolderInODFRackInCO1_ShouldSucceed()
        {
            // Setup
            var sutNodeContainer = TestUtilityNetwork.NodeContainer_CO_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainer, out var nodeContainerBeforeCommand);

            var placeEquipmentCmd = new PlaceTerminalEquipmentInNodeContainer(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                nodeContainerId: sutNodeContainer,
                Guid.NewGuid(),
                terminalEquipmentSpecificationId: TestSpecifications.LISA_SplitterHolder,
                numberOfEquipments: 1,
                startSequenceNumber: 1,
                namingMethod: TerminalEquipmentNamingMethodEnum.NameOnly,
                namingInfo: new Events.Core.Infos.NamingInfo() { Name = "1:32 Split" }
            )
            {
                SubrackPlacementInfo = new SubrackPlacementInfo(nodeContainerBeforeCommand.Racks[0].Id, 84, SubrackPlacmentMethod.BottomUp)
            };


            // Act
            var placeEquipmentCmdResult = await _commandDispatcher.HandleAsync<PlaceTerminalEquipmentInNodeContainer, Result>(placeEquipmentCmd);

            var nodeContainerQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new EquipmentIdList() { placeEquipmentCmd.NodeContainerId })
            );

            var nodeContainer = nodeContainerQueryResult.Value.NodeContainers.First();


            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new EquipmentIdList(nodeContainer.Racks[0].SubrackMounts.Select(s => s.TerminalEquipmentId)))
            );

            // Get last mount which should be the splitter placed at the top
            var splitterMount = nodeContainer.Racks[0].SubrackMounts.Last();
            var sutEquipment = equipmentQueryResult.Value.TerminalEquipment[splitterMount.TerminalEquipmentId];

            sutEquipment.Name.Should().Be("1:32 Split");
            sutEquipment.TerminalStructures.Length.Should().Be(0);

            // Assert
            placeEquipmentCmdResult.IsSuccess.Should().BeTrue();
        }

        [Fact, Order(7)]
        public async Task PlaceSplitterInLISASplitterHolderInODFRackInCO1_ShouldSucceed()
        {
            // Setup
            var sutNodeContainer = TestUtilityNetwork.NodeContainer_CO_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainer, out var nodeContainerBeforeCommand);

            // Get last mount which should be the splitter placed at the top
            var splitterMount = nodeContainerBeforeCommand.Racks[0].SubrackMounts.Last();

            utilityNetwork.TryGetEquipment<TerminalEquipment>(splitterMount.TerminalEquipmentId, out var terminalEquipmentBeforeUpdate);

            var placeEquipmentCmd = new PlaceAdditionalStructuresInTerminalEquipment(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CO_1,
                terminalEquipmentId: terminalEquipmentBeforeUpdate.Id,
                structureSpecificationId: TestSpecifications.LISA_1_32_Splitter,
                position: 1,
                numberOfStructures: 4
            );
           
            // Act
            var placeEquipmentCmdResult = await _commandDispatcher.HandleAsync<PlaceAdditionalStructuresInTerminalEquipment, Result>(placeEquipmentCmd);

            placeEquipmentCmdResult.IsSuccess.Should().BeTrue();

            utilityNetwork.TryGetEquipment<TerminalEquipment>(splitterMount.TerminalEquipmentId, out var terminalEquipmentAfterUpdate);
        
            terminalEquipmentAfterUpdate.TerminalStructures.Length.Should().Be(4);

            utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphElement>(terminalEquipmentAfterUpdate.TerminalStructures.First().Terminals.First().Id, out var firstTerminalInGraph);

            firstTerminalInGraph.Should().NotBeNull();
        }

        [Fact, Order(10)]
        public async Task PlaceDataRackInCO_1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainerBeforeCommand);

            var placeRackCmd = new PlaceRackInNodeContainer(
                Guid.NewGuid(),
                new UserContext("test", Guid.Empty),
                sutNodeContainerId,
                Guid.NewGuid(),
                TestSpecifications.Rack_ESTI,
                "DATA",
                60
            );

            var placeRackResult = await _commandDispatcher.HandleAsync<PlaceRackInNodeContainer, Result>(placeRackCmd);

            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new InterestIdList() { nodeContainerBeforeCommand.InterestId })
            );

            // Assert
            placeRackResult.IsSuccess.Should().BeTrue();
        }

        [Fact, Order(11)]
        public async Task PlaceOltDataRackInCO1_ShouldSucceed()
        {
            // Setup
            var sutNodeContainer = TestUtilityNetwork.NodeContainer_CO_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainer, out var nodeContainerBeforeCommand);

            var placeEquipmentCmd = new PlaceTerminalEquipmentInNodeContainer(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                nodeContainerId: sutNodeContainer,
                Guid.NewGuid(),
                terminalEquipmentSpecificationId: TestSpecifications.OLT,
                numberOfEquipments: 1,
                startSequenceNumber: 1,
                namingMethod: TerminalEquipmentNamingMethodEnum.NameOnly,
                namingInfo: new Events.Core.Infos.NamingInfo() { Name = "OLT 1" }
            )
            {
                SubrackPlacementInfo = new SubrackPlacementInfo(nodeContainerBeforeCommand.Racks[1].Id, 30, SubrackPlacmentMethod.BottomUp)
            };

            // Act
            var placeEquipmentCmdResult = await _commandDispatcher.HandleAsync<PlaceTerminalEquipmentInNodeContainer, Result>(placeEquipmentCmd);

            // Assert
            placeEquipmentCmdResult.IsSuccess.Should().BeTrue();
        }

        [Fact, Order(12)]
        public async Task PlaceLineCardInOltInDataRackInCO1_ShouldSucceed()
        {
            // Setup
            var sutNodeContainer = TestUtilityNetwork.NodeContainer_CO_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainer, out var nodeContainerBeforeCommand);

            // Get olt
            var olt = nodeContainerBeforeCommand.Racks[1].SubrackMounts.First(s => s.Position == 30);

            utilityNetwork.TryGetEquipment<TerminalEquipment>(olt.TerminalEquipmentId, out var terminalEquipmentBeforeUpdate);

            var placeEquipmentCmd = new PlaceAdditionalStructuresInTerminalEquipment(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CO_1,
                terminalEquipmentId: terminalEquipmentBeforeUpdate.Id,
                structureSpecificationId: TestSpecifications.OLT_LineCard16Port,
                position: 1,
                numberOfStructures: 1
            );

            // Act
            var placeEquipmentCmdResult = await _commandDispatcher.HandleAsync<PlaceAdditionalStructuresInTerminalEquipment, Result>(placeEquipmentCmd);

            placeEquipmentCmdResult.IsSuccess.Should().BeTrue();
        }



        [Fact, Order(13)]
        public async Task PlaceEmptyLgxHolderInDataRackInCO1_ShouldSucceed()
        {
            // Setup
            var sutNodeContainer = TestUtilityNetwork.NodeContainer_CO_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainer, out var nodeContainerBeforeCommand);

            var placeEquipmentCmd = new PlaceTerminalEquipmentInNodeContainer(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                nodeContainerId: sutNodeContainer,
                Guid.NewGuid(),
                terminalEquipmentSpecificationId: TestSpecifications.LGX_Holder,
                numberOfEquipments: 1,
                startSequenceNumber: 1,
                namingMethod: TerminalEquipmentNamingMethodEnum.NameOnly,
                namingInfo: new Events.Core.Infos.NamingInfo() { Name = "LGX Holder" }
            )
            {
                SubrackPlacementInfo = new SubrackPlacementInfo(nodeContainerBeforeCommand.Racks[1].Id, 10, SubrackPlacmentMethod.BottomUp)
            };

            // Act
            var placeEquipmentCmdResult = await _commandDispatcher.HandleAsync<PlaceTerminalEquipmentInNodeContainer, Result>(placeEquipmentCmd);
    
            // Assert
            placeEquipmentCmdResult.IsSuccess.Should().BeTrue();
        }

        [Fact, Order(14)]
        public async Task PlaceWdmInLgxHolderInDataRackInCO1_ShouldSucceed()
        {
            // Setup
            var sutNodeContainer = TestUtilityNetwork.NodeContainer_CO_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainer, out var nodeContainerBeforeCommand);

            // Get LGX holder at position 10
            var splitterMount = nodeContainerBeforeCommand.Racks[1].SubrackMounts.First(s => s.Position == 10);

            utilityNetwork.TryGetEquipment<TerminalEquipment>(splitterMount.TerminalEquipmentId, out var terminalEquipmentBeforeUpdate);

            var placeEquipmentCmd = new PlaceAdditionalStructuresInTerminalEquipment(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CO_1,
                terminalEquipmentId: terminalEquipmentBeforeUpdate.Id,
                structureSpecificationId: TestSpecifications.LGX_WDMType1,
                position: 1,
                numberOfStructures: 1
            );

            // Act
            var placeEquipmentCmdResult = await _commandDispatcher.HandleAsync<PlaceAdditionalStructuresInTerminalEquipment, Result>(placeEquipmentCmd);

            placeEquipmentCmdResult.IsSuccess.Should().BeTrue();
        }

        [Fact, Order(16)]
        public async Task PlaceWdmInLgxHolderInDataRackInCO1OnPosUsed_ShouldFail()
        {
            // Setup
            var sutNodeContainer = TestUtilityNetwork.NodeContainer_CO_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainer, out var nodeContainerBeforeCommand);

            // Get LGX holder at position 10
            var splitterMount = nodeContainerBeforeCommand.Racks[1].SubrackMounts.First(s => s.Position == 10);

            utilityNetwork.TryGetEquipment<TerminalEquipment>(splitterMount.TerminalEquipmentId, out var terminalEquipmentBeforeUpdate);

            var placeEquipmentCmd = new PlaceAdditionalStructuresInTerminalEquipment(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CO_1,
                terminalEquipmentId: terminalEquipmentBeforeUpdate.Id,
                structureSpecificationId: TestSpecifications.LGX_WDMType1,
                position: 1,
                numberOfStructures: 1
            );

            // Act
            var placeEquipmentCmdResult = await _commandDispatcher.HandleAsync<PlaceAdditionalStructuresInTerminalEquipment, Result>(placeEquipmentCmd);

            placeEquipmentCmdResult.IsSuccess.Should().BeFalse();
        }


        [Fact, Order(50)]
        public async Task RemoveSplitter3InLISASplitterHolderInODFRackInCO1_ShouldSucceed()
        {
            // Setup
            var sutNodeContainer = TestUtilityNetwork.NodeContainer_CO_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainer, out var nodeContainerBeforeCommand);

            // Get last mount which should be the splitter placed at the top
            var splitterMount = nodeContainerBeforeCommand.Racks[0].SubrackMounts.Last();

            utilityNetwork.TryGetEquipment<TerminalEquipment>(splitterMount.TerminalEquipmentId, out var terminalEquipmentBeforeUpdate);

            var terminalStructureToRemove = terminalEquipmentBeforeUpdate.TerminalStructures[2];

            var removeStructure = new RemoveTerminalStructureFromTerminalEquipment(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CO_1,
                terminalEquipmentId: terminalEquipmentBeforeUpdate.Id,
                terminalStructureId: terminalStructureToRemove.Id
            );

            // Act
            var removeStructureResult = await _commandDispatcher.HandleAsync<RemoveTerminalStructureFromTerminalEquipment, Result>(removeStructure);

            removeStructureResult.IsSuccess.Should().BeTrue();

            utilityNetwork.TryGetEquipment<TerminalEquipment>(splitterMount.TerminalEquipmentId, out var terminalEquipmentAfterUpdate);

            // Check that removed structure is marked as deleted
            terminalEquipmentAfterUpdate.TerminalStructures.First(t => t.Id == terminalStructureToRemove.Id).Deleted.Should().BeTrue();

            // Check that the rest is not marked as deleted
            terminalEquipmentAfterUpdate.TerminalStructures.First(t => t.Id != terminalStructureToRemove.Id).Deleted.Should().BeFalse();

            // Check that terminal of removed structure are removed from graph
            foreach (var terminal in terminalStructureToRemove.Terminals)
            {
                utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphElement>(terminal.Id, out var _).Should().BeFalse();
            }

            // Check that terminals of non-removed structure are still in graph
            foreach (var terminal in terminalEquipmentAfterUpdate.TerminalStructures[0].Terminals)
            {
                utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphElement>(terminal.Id, out var _).Should().BeTrue();
            }
        }


        [Fact, Order(51)]
        public async Task PlaceSplitter3InLISASplitterHolderInODFRackInCO1_ShouldSucceed()
        {
            // Setup
            var sutNodeContainer = TestUtilityNetwork.NodeContainer_CO_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainer, out var nodeContainerBeforeCommand);

            // Get last mount which should be the splitter placed at the top
            var splitterMount = nodeContainerBeforeCommand.Racks[0].SubrackMounts.Last();

            utilityNetwork.TryGetEquipment<TerminalEquipment>(splitterMount.TerminalEquipmentId, out var terminalEquipmentBeforeUpdate);

            var placeEquipmentCmd = new PlaceAdditionalStructuresInTerminalEquipment(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CO_1,
                terminalEquipmentId: terminalEquipmentBeforeUpdate.Id,
                structureSpecificationId: TestSpecifications.LISA_1_32_Splitter,
                position: 3,
                numberOfStructures: 1
            );

            // Act
            var placeEquipmentCmdResult = await _commandDispatcher.HandleAsync<PlaceAdditionalStructuresInTerminalEquipment, Result>(placeEquipmentCmd);

            placeEquipmentCmdResult.IsSuccess.Should().BeTrue();

            utilityNetwork.TryGetEquipment<TerminalEquipment>(splitterMount.TerminalEquipmentId, out var terminalEquipmentAfterUpdate);

            // 5 because one is in deleted state
            terminalEquipmentAfterUpdate.TerminalStructures.Length.Should().Be(5);

            utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphElement>(terminalEquipmentAfterUpdate.TerminalStructures.First().Terminals.First().Id, out var firstTerminalInGraph);

            firstTerminalInGraph.Should().NotBeNull();
        }

        [Fact, Order(60)]
        public async Task PlaceLineCardWithInterfaceInOltInDataRackInCO1_ShouldSucceed()
        {
            // Setup
            var sutNodeId = TestRouteNetwork.CO_1;
            var sutNodeContainer = TestUtilityNetwork.NodeContainer_CO_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainer, out var nodeContainerBeforeCommand);

            // Get olt
            var olt = nodeContainerBeforeCommand.Racks[1].SubrackMounts.First(s => s.Position == 30);

            utilityNetwork.TryGetEquipment<TerminalEquipment>(olt.TerminalEquipmentId, out var terminalEquipmentBeforeUpdate);

            var placeEquipmentCmd = new PlaceAdditionalStructuresInTerminalEquipment(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: TestRouteNetwork.CO_1,
                terminalEquipmentId: terminalEquipmentBeforeUpdate.Id,
                structureSpecificationId: TestSpecifications.OLT_InterfaceModule,
                position: 10,
                numberOfStructures: 1
            )
            {
                InterfaceInfo = new InterfaceInfo("xe", 5, 5, 5, "FK123456")
            };

            // Act
            var placeEquipmentCmdResult = await _commandDispatcher.HandleAsync<PlaceAdditionalStructuresInTerminalEquipment, Result>(placeEquipmentCmd);
            placeEquipmentCmdResult.IsSuccess.Should().BeTrue();

            var placeEquipmentCmd2 = new PlaceAdditionalStructuresInTerminalEquipment(
              correlationId: Guid.NewGuid(),
              userContext: new UserContext("test", Guid.Empty),
              routeNodeId: TestRouteNetwork.CO_1,
              terminalEquipmentId: terminalEquipmentBeforeUpdate.Id,
              structureSpecificationId: TestSpecifications.OLT_InterfaceModule,
              position: 10,
              numberOfStructures: 1
          )
            {
                InterfaceInfo = new InterfaceInfo("xe", 5, 5, 6, "FK2222222")
            };

            // Act
            var placeEquipmentCmdResult2 = await _commandDispatcher.HandleAsync<PlaceAdditionalStructuresInTerminalEquipment, Result>(placeEquipmentCmd2);
            placeEquipmentCmdResult2.IsSuccess.Should().BeTrue();



            utilityNetwork.TryGetEquipment<TerminalEquipment>(olt.TerminalEquipmentId, out var oltEquipment);

            var firstInterface = oltEquipment.TerminalStructures.First(t => t.interfaceInfo != null).Terminals[0].Id;

            // Get lisa tray 80
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainerBeforeCommand.Racks[0].SubrackMounts.First(s => s.Position == 79).TerminalEquipmentId, out var lisaTray80);

            // Connect olt interface with lisa tray 80 pin 1
            var connectCmd = new ConnectTerminalsAtRouteNode(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                routeNodeId: sutNodeId,
                fromTerminalId: firstInterface,
                toTerminalId: lisaTray80.TerminalStructures[0].Terminals[0].Id,
                fiberCoordLength: 100.0
            );

            var connectCmdResult = await _commandDispatcher.HandleAsync<ConnectTerminalsAtRouteNode, Result>(connectCmd);

            // Assert
            connectCmdResult.IsSuccess.Should().BeTrue();


        }


    }
}

#nullable enable
