using FluentAssertions;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions.Ordering;

namespace OpenFTTH.UtilityGraphService.Tests.UtilityNetwork
{
    [Order(121)]
    public class T0121_TerminalSpecificationTests
    {
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;

        public T0121_TerminalSpecificationTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
        }

        [Fact]
        public async Task AddSimpleTerminalEquipmentSpecification_ShouldSucceed()
        {
            // Create manufacturer
            var manufacturer = new Manufacturer(Guid.NewGuid(), "Super Manufacturer");
            await _commandDispatcher.HandleAsync<AddManufacturer, Result>(new AddManufacturer(Guid.NewGuid(), new UserContext("test", Guid.Empty), manufacturer));

            var terminalEquipmentSpecification = new TerminalEquipmentSpecification(Guid.NewGuid(), "SpliceClosure", "BUDI 1s deluxe", "BUDI", false, 0,
                new TerminalStructureTemplate[] { }
            )
            {
                Description = "asdasasdasd",
                ManufacturerRefs = new Guid[] { manufacturer.Id }
            };

            // Act
            var addSpecificationCommandResult = await _commandDispatcher.HandleAsync<AddTerminalEquipmentSpecification, Result>(new AddTerminalEquipmentSpecification(Guid.NewGuid(), new UserContext("test", Guid.Empty), terminalEquipmentSpecification));

            var equipmentSpecificationsQueryResult = await _queryDispatcher.HandleAsync<GetTerminalEquipmentSpecifications, Result<LookupCollection<TerminalEquipmentSpecification>>>(new GetTerminalEquipmentSpecifications());

            // Assert
            addSpecificationCommandResult.IsSuccess.Should().BeTrue();
            equipmentSpecificationsQueryResult.IsSuccess.Should().BeTrue();

        }

        [Fact]
        public async Task AddSimpleTerminalStructureSpecification_ShouldSucceed()
        {
            // Create manufacturer
            var manufacturer = new Manufacturer(Guid.NewGuid(), "Tray Manufacturer");
            await _commandDispatcher.HandleAsync<AddManufacturer, Result>(new AddManufacturer(Guid.NewGuid(), new UserContext("test", Guid.Empty), manufacturer));

            var TerminalStructureSpecification = new TerminalStructureSpecification(Guid.NewGuid(), "tray", "tray deluxe", "BUDI", 
                new TerminalTemplate[] { }
            )
            {
                Description = "asdasasdasd",
                ManufacturerRefs = new Guid[] { manufacturer.Id }
            };

            // Act
            var addSpecificationCommandResult = await _commandDispatcher.HandleAsync<AddTerminalStructureSpecification, Result>(new AddTerminalStructureSpecification(Guid.NewGuid(), new UserContext("test", Guid.Empty), TerminalStructureSpecification));

            var equipmentSpecificationsQueryResult = await _queryDispatcher.HandleAsync<GetTerminalStructureSpecifications, Result<LookupCollection<TerminalStructureSpecification>>>(new GetTerminalStructureSpecifications());

            // Assert
            addSpecificationCommandResult.IsSuccess.Should().BeTrue();
            equipmentSpecificationsQueryResult.IsSuccess.Should().BeTrue();

        }
    }
}
