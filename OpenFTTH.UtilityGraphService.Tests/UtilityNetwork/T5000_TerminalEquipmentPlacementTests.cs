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
    [Order(5000)]
    public class T5000_TerminalEquipmentPlacementTests
    {
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;

        public T5000_TerminalEquipmentPlacementTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public async Task PlaceFirstTerminalEquipmentInCC1_ShouldSucceed()
        {
            var placeEquipmentCmd = new PlaceTerminalEquipmentInNodeContainer(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                nodeContainerId: TestUtilityNetwork.NodeContainer_CC_1,
                Guid.NewGuid(),
                terminalEquipmentSpecificationId: TestSpecifications.SpliceClosure_BUDI1S_16SCTrays,
                numberOfEquipments: 1,
                startSequenceNumber: 1,
                namingMethod: TerminalEquipmentNamingMethodEnum.NameOnly,
                namingInfo: new NamingInfo("CC1 Splice Closure 1", null)
            )
            {
                AddressInfo = new AddressInfo(Guid.NewGuid(), Guid.NewGuid(), "hej")
            };

            // Act
            var placeEquipmentCmdResult = await _commandDispatcher.HandleAsync<PlaceTerminalEquipmentInNodeContainer, Result>(placeEquipmentCmd);

            var nodeContainerQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new EquipmentIdList() { placeEquipmentCmd.NodeContainerId })
            );

            var nodeContainer = nodeContainerQueryResult.Value.NodeContainers.First();


            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new EquipmentIdList() { nodeContainer.TerminalEquipmentReferences.First() })
            );

            var equipment = equipmentQueryResult.Value.TerminalEquipment.First();


            // Assert
            placeEquipmentCmdResult.IsSuccess.Should().BeTrue();
            nodeContainerQueryResult.IsSuccess.Should().BeTrue();
            equipmentQueryResult.IsSuccess.Should().BeTrue();

            // Check that address info is set
            equipment.AddressInfo.Should().Be(placeEquipmentCmd.AddressInfo);

            // Check that terminal is placed is placed in node container
            nodeContainer.TerminalEquipmentReferences.Count().Should().Be(1);

            equipment.SpecificationId.Should().Be(placeEquipmentCmd.TerminalEquipmentSpecificationId);
            equipment.NodeContainerId.Should().Be(placeEquipmentCmd.NodeContainerId);
            equipment.Name.Should().Be("CC1 Splice Closure 1");

            // test that terminal structures and terminal has been created
            equipment.TerminalStructures.Count().Should().Be(16);
            equipment.TerminalStructures[0].Position.Should().Be(1);
            equipment.TerminalStructures[15].Position.Should().Be(16);
            equipment.TerminalStructures[0].Terminals.Count().Should().Be(12);
            equipment.TerminalStructures[0].Terminals[0].Name.Should().Be("1");
            equipment.TerminalStructures[0].Terminals[11].Name.Should().Be("12");
            equipment.TerminalStructures[0].Terminals[0].IsSplice.Should().BeTrue();
            equipment.TerminalStructures[0].Terminals[0].IsPigtail.Should().BeFalse();
            equipment.TerminalStructures[0].Terminals[0].ConnectorType.Should().BeNull();

        }

        [Fact, Order(2)]
        public async Task PlaceTwoMoreTerminalEquipmentsInCC1_ShouldSucceed()
        {
            var placeEquipmentCmd = new PlaceTerminalEquipmentInNodeContainer(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                nodeContainerId: TestUtilityNetwork.NodeContainer_CC_1,
                Guid.NewGuid(),
                terminalEquipmentSpecificationId: TestSpecifications.SpliceClosure_BUDI1S_16SCTrays,
                numberOfEquipments: 2,
                startSequenceNumber: 5,
                namingMethod: TerminalEquipmentNamingMethodEnum.NameAndNumber,
                namingInfo: new NamingInfo("CC1 Splice Closure", null)
            );

            // Act
            var placeEquipmentCmdResult = await _commandDispatcher.HandleAsync<PlaceTerminalEquipmentInNodeContainer, Result>(placeEquipmentCmd);

            var nodeContainerQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new EquipmentIdList() { placeEquipmentCmd.NodeContainerId })
            );

            var nodeContainer = nodeContainerQueryResult.Value.NodeContainers.First();


            var equipmentQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new EquipmentIdList() { nodeContainer.TerminalEquipmentReferences.Last() })
            );

            var lastEquipment = equipmentQueryResult.Value.TerminalEquipment.First();


            // Assert
            placeEquipmentCmdResult.IsSuccess.Should().BeTrue();
            nodeContainerQueryResult.IsSuccess.Should().BeTrue();
            equipmentQueryResult.IsSuccess.Should().BeTrue();

            nodeContainer.TerminalEquipmentReferences.Count().Should().Be(3);

            lastEquipment.SpecificationId.Should().Be(placeEquipmentCmd.TerminalEquipmentSpecificationId);
            lastEquipment.NodeContainerId.Should().Be(placeEquipmentCmd.NodeContainerId);
            lastEquipment.Name.Should().Be("CC1 Splice Closure 6");
        }


        [Fact, Order(10)]
        public async Task PlaceFirstTwoTerminalEquipmentInJ1Rack1_ShouldSucceed()
        {
            // Setup
            var sutNodeContainer = TestUtilityNetwork.NodeContainer_J_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainer, out var nodeContainerBeforeCommand);

            var placeEquipmentCmd = new PlaceTerminalEquipmentInNodeContainer(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                nodeContainerId: sutNodeContainer,
                Guid.NewGuid(),
                terminalEquipmentSpecificationId: TestSpecifications.Subrack_LISA_APC_UPC,
                numberOfEquipments: 2,
                startSequenceNumber: 1,
                namingMethod: TerminalEquipmentNamingMethodEnum.NumberOnly,
                namingInfo: null
            )
            {
                SubrackPlacementInfo = new SubrackPlacementInfo(nodeContainerBeforeCommand.Racks[0].Id, 4, SubrackPlacmentMethod.BottomUp)
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

            var firstMount = nodeContainer.Racks[0].SubrackMounts[0];
            var firstEquipment = equipmentQueryResult.Value.TerminalEquipment[firstMount.TerminalEquipmentId];

            var secondMount = nodeContainer.Racks[0].SubrackMounts[1];
            var secondEquipment = equipmentQueryResult.Value.TerminalEquipment[secondMount.TerminalEquipmentId];


            // Assert
            placeEquipmentCmdResult.IsSuccess.Should().BeTrue();
            nodeContainerQueryResult.IsSuccess.Should().BeTrue();
            equipmentQueryResult.IsSuccess.Should().BeTrue();

            nodeContainer.TerminalEquipmentReferences.Should().BeNull();
            nodeContainer.Racks[0].SubrackMounts.Count().Should().Be(2);

            firstMount.Position.Should().Be(4);
            firstMount.HeightInUnits.Should().Be(1);

            firstEquipment.Name.Should().Be("1");
            firstEquipment.SpecificationId.Should().Be(placeEquipmentCmd.TerminalEquipmentSpecificationId);
            firstEquipment.NodeContainerId.Should().Be(placeEquipmentCmd.NodeContainerId);

            secondMount.Position.Should().Be(5);
            secondMount.HeightInUnits.Should().Be(1);

            secondEquipment.Name.Should().Be("2");
            secondEquipment.SpecificationId.Should().Be(placeEquipmentCmd.TerminalEquipmentSpecificationId);
            secondEquipment.NodeContainerId.Should().Be(placeEquipmentCmd.NodeContainerId);

        }


        [Fact, Order(11)]
        public async Task PlaceThirdTerminalEquipmentInJ1Rack1_ShouldSucceed()
        {
            // Setup
            var sutNodeContainer = TestUtilityNetwork.NodeContainer_J_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainer, out var nodeContainerBeforeCommand);

            var placeEquipmentCmd = new PlaceTerminalEquipmentInNodeContainer(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                nodeContainerId: sutNodeContainer,
                Guid.NewGuid(),
                terminalEquipmentSpecificationId: TestSpecifications.Subrack_GPS_144_LC,
                numberOfEquipments: 1,
                startSequenceNumber: 5,
                namingMethod: TerminalEquipmentNamingMethodEnum.NumberOnly,
                namingInfo: null
            )
            {
                SubrackPlacementInfo = new SubrackPlacementInfo(nodeContainerBeforeCommand.Racks[0].Id, 0, SubrackPlacmentMethod.BottomUp)
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

            var firstMount = nodeContainer.Racks[0].SubrackMounts[2];
            var firstEquipment = equipmentQueryResult.Value.TerminalEquipment[firstMount.TerminalEquipmentId];

            var secondMount = nodeContainer.Racks[0].SubrackMounts[0];
            var secondEquipment = equipmentQueryResult.Value.TerminalEquipment[secondMount.TerminalEquipmentId];

            var thirdMount = nodeContainer.Racks[0].SubrackMounts[1];
            var thirdEquipment = equipmentQueryResult.Value.TerminalEquipment[thirdMount.TerminalEquipmentId];


            // Assert
            placeEquipmentCmdResult.IsSuccess.Should().BeTrue();
            nodeContainerQueryResult.IsSuccess.Should().BeTrue();
            equipmentQueryResult.IsSuccess.Should().BeTrue();

            nodeContainer.TerminalEquipmentReferences.Should().BeNull();
            nodeContainer.Racks[0].SubrackMounts.Count().Should().Be(3);


            // First equipment/mount
            firstMount.Position.Should().Be(0);
            firstMount.HeightInUnits.Should().Be(4);

            firstEquipment.Name.Should().Be("5");
            firstEquipment.SpecificationId.Should().Be(placeEquipmentCmd.TerminalEquipmentSpecificationId);
            firstEquipment.NodeContainerId.Should().Be(placeEquipmentCmd.NodeContainerId);


            // Second equipment/mount
            secondMount.Position.Should().Be(4);
            secondMount.HeightInUnits.Should().Be(1);

            secondEquipment.Name.Should().Be("1");
            secondEquipment.NodeContainerId.Should().Be(placeEquipmentCmd.NodeContainerId);


            // Third equipment/mount
            thirdMount.Position.Should().Be(5);
            thirdMount.HeightInUnits.Should().Be(1);

            thirdEquipment.Name.Should().Be("2");
            thirdEquipment.NodeContainerId.Should().Be(placeEquipmentCmd.NodeContainerId);
        }

        [Fact, Order(12)]
        public async Task PlaceTerminalEquipmentInCO1_ShouldSucceed()
        {
            var placeEquipmentCmd = new PlaceTerminalEquipmentInNodeContainer(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                nodeContainerId: TestUtilityNetwork.NodeContainer_CO_1,
                Guid.NewGuid(),
                terminalEquipmentSpecificationId: TestSpecifications.SpliceClosure_BUDI1S_16SCTrays,
                numberOfEquipments: 1,
                startSequenceNumber: 1,
                namingMethod: TerminalEquipmentNamingMethodEnum.NameAndNumber,
                namingInfo: new NamingInfo("Splice Closure", null)
            );

            // Act
            var placeEquipmentCmdResult = await _commandDispatcher.HandleAsync<PlaceTerminalEquipmentInNodeContainer, Result>(placeEquipmentCmd);

            var nodeContainerQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new EquipmentIdList() { placeEquipmentCmd.NodeContainerId })
            );

            // Assert
            placeEquipmentCmdResult.IsSuccess.Should().BeTrue();
        }


        [Fact, Order(50)]
        public async Task PlaceCustomerTerminalEquipmentInSDU1_ShouldSucceed()
        {
            var placeEquipmentCmd = new PlaceTerminalEquipmentInNodeContainer(
                correlationId: Guid.NewGuid(),
                userContext: new UserContext("test", Guid.Empty),
                nodeContainerId: TestUtilityNetwork.NodeContainer_SDU_1,
                Guid.NewGuid(),
                terminalEquipmentSpecificationId: TestSpecifications.CustomerTermination,
                numberOfEquipments: 1,
                startSequenceNumber: 1,
                namingMethod: TerminalEquipmentNamingMethodEnum.NameOnly,
                namingInfo: new NamingInfo("C12345678", null)
            )
            {
                AddressInfo = new AddressInfo()
                {
                    UnitAddressId = Guid.Parse("0a3f50bc-aa89-32b8-e044-0003ba298018")
                }
            };

            // Act
            var placeEquipmentCmdResult = await _commandDispatcher.HandleAsync<PlaceTerminalEquipmentInNodeContainer, Result>(placeEquipmentCmd);

            var nodeContainerQueryResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(
                new GetEquipmentDetails(new EquipmentIdList() { placeEquipmentCmd.NodeContainerId })
            );

            // Assert
            placeEquipmentCmdResult.IsSuccess.Should().BeTrue();
        }



        [Fact, Order(100)]
        public async Task QueryConnectivityInfoOneFirstTerminalEquipmentInJ1Rack1_ShouldSucceed()
        {
            // Setup
            var sutRouteNodeId = TestRouteNetwork.J_1;

            var sutNodeContainer = TestUtilityNetwork.NodeContainer_J_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainer, out var nodeContainerBeforeCommand);

            var firstEquipmentInRack1Id = nodeContainerBeforeCommand.Racks[0].SubrackMounts[2].TerminalEquipmentId;

            var connectivityQuery = new GetTerminalEquipmentConnectivityView(sutRouteNodeId, firstEquipmentInRack1Id);


            // Act
            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetTerminalEquipmentConnectivityView, Result<TerminalEquipmentAZConnectivityViewModel>>(
                connectivityQuery
            );

            // Assert
            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;

            viewModel.TerminalEquipments.Should().NotBeNull();
            viewModel.TerminalEquipments.Count().Should().Be(1);

            viewModel.TerminalEquipments[0].TerminalStructures.Count().Should().Be(6);
            viewModel.TerminalEquipments[0].TerminalStructures[0].Lines.Count().Should().Be(24);
            viewModel.TerminalEquipments[0].TerminalStructures[0].Lines.Count(l => l.A != null && l.Z != null).Should().Be(24);
            viewModel.TerminalEquipments[0].TerminalStructures[0].Lines.Count(l => l.A.Terminal != null && l.Z.Terminal != null).Should().Be(24);
            viewModel.TerminalEquipments[0].TerminalStructures[0].Lines.Count(l => l.A.Terminal.Id != Guid.Empty && l.Z.Terminal.Id != Guid.Empty).Should().Be(24);
        }

        [Fact, Order(101)]
        public async Task QueryConnectivityInfoOneAllTerminalEquipmentInJ1Rack1_ShouldSucceed()
        {
            // Setup
            var sutRouteNodeId = TestRouteNetwork.J_1;

            var sutNodeContainer = TestUtilityNetwork.NodeContainer_J_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainer, out var nodeContainerBeforeCommand);

            var firstEquipmentInRack1Id = nodeContainerBeforeCommand.Racks[0].Id;

            var connectivityQuery = new GetTerminalEquipmentConnectivityView(sutRouteNodeId, firstEquipmentInRack1Id);


            // Act
            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetTerminalEquipmentConnectivityView, Result<TerminalEquipmentAZConnectivityViewModel>>(
                connectivityQuery
            );

            // Assert
            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;

            viewModel.ParentNodeStructures.Should().NotBeNull();
            viewModel.ParentNodeStructures.Count().Should().Be(1);

            viewModel.TerminalEquipments.Should().NotBeNull();
            viewModel.TerminalEquipments.Count().Should().Be(3);

            // check parent rack
            viewModel.ParentNodeStructures.Count().Should().Be(1);

            var gpsTerminalEquipment = viewModel.TerminalEquipments.First(t => t.SpecName.Contains("GPS"));

            // check first equipmenmt
            gpsTerminalEquipment.ParentNodeStructureId.Should().Be(nodeContainerBeforeCommand.Racks[0].Id);
            gpsTerminalEquipment.TerminalStructures.Count().Should().Be(6);
            gpsTerminalEquipment.TerminalStructures[0].Lines.Count().Should().Be(24);
            gpsTerminalEquipment.TerminalStructures[0].Lines.Count(l => l.A != null && l.Z != null).Should().Be(24);
            gpsTerminalEquipment.TerminalStructures[0].Lines.Count(l => l.A.Terminal != null && l.Z.Terminal != null).Should().Be(24);
            gpsTerminalEquipment.TerminalStructures[0].Lines.Count(l => l.A.Terminal.Id != Guid.Empty && l.Z.Terminal.Id != Guid.Empty).Should().Be(24);
        }
    }
}
