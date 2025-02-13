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
    [Order(120)]
    public class T0120_NodeContainerSpecificationTests
    {
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;

        public T0120_NodeContainerSpecificationTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
        }

        [Fact]
        public async Task AddValidNodeContainerSpecification_ShouldSucceed()
        {
            // Create manufacturer
            var manufacturer = new Manufacturer(Guid.NewGuid(), "Node Container Manufacturer");
            await _commandDispatcher.HandleAsync<AddManufacturer, Result>(new AddManufacturer(Guid.NewGuid(), new UserContext("test", Guid.Empty), manufacturer));

            // Setup a node equipment container specification
            var newNodeContainerSpecification = new NodeContainerSpecification(Guid.NewGuid(), "ManHoles", "Draka xyz")
            {
                Description = "Draka super duper xyz",
                ManufacturerRefs = new Guid[] { manufacturer.Id }
            };

            // Act
            var addNodeSpecificationCommandResult = await _commandDispatcher.HandleAsync<AddNodeContainerSpecification, Result>(new AddNodeContainerSpecification(Guid.NewGuid(), new UserContext("test", Guid.Empty), newNodeContainerSpecification));

            var nodeContainerSpecificationsQueryResult = await _queryDispatcher.HandleAsync<GetNodeContainerSpecifications, Result<LookupCollection<NodeContainerSpecification>>>(new GetNodeContainerSpecifications());

            // Assert
            addNodeSpecificationCommandResult.IsSuccess.Should().BeTrue();
            nodeContainerSpecificationsQueryResult.IsSuccess.Should().BeTrue();
            nodeContainerSpecificationsQueryResult.Value[newNodeContainerSpecification.Id].Name.Should().Be(newNodeContainerSpecification.Name);
            nodeContainerSpecificationsQueryResult.Value[newNodeContainerSpecification.Id].Description.Should().Be(newNodeContainerSpecification.Description);
        }

        [Fact]
        public async Task AddValidRackSpecification_ShouldSucceed()
        {
            // Create manufacturer
            var manufacturer = new Manufacturer(Guid.NewGuid(), "Rack Manufacturer");
            await _commandDispatcher.HandleAsync<AddManufacturer, Result>(new AddManufacturer(Guid.NewGuid(), new UserContext("test", Guid.Empty), manufacturer));

            var newRackSpecification = new RackSpecification(Guid.NewGuid(), "ETSI", "ETSI")
            {
                Description = "ETSI Rack",
            };

            // Act
            var addRackSpecificationCommandResult = await _commandDispatcher.HandleAsync<AddRackSpecification, Result>(new AddRackSpecification(Guid.NewGuid(), new UserContext("test", Guid.Empty), newRackSpecification));

            var rackSpecificationsQueryResult = await _queryDispatcher.HandleAsync<GetRackSpecifications, Result<LookupCollection<RackSpecification>>>(new GetRackSpecifications());

            // Assert
            addRackSpecificationCommandResult.IsSuccess.Should().BeTrue();
            rackSpecificationsQueryResult.IsSuccess.Should().BeTrue();
            rackSpecificationsQueryResult.Value[newRackSpecification.Id].Name.Should().Be(newRackSpecification.Name);
            rackSpecificationsQueryResult.Value[newRackSpecification.Id].Description.Should().Be(newRackSpecification.Description);
        }

        [Fact]
        public async Task AddTwoRackSpecificationsWithSameName_ShouldFail()
        {
            // Create manufacturer
            var manufacturer = new Manufacturer(Guid.NewGuid(), "Rack Manufacturer");
            await _commandDispatcher.HandleAsync<AddManufacturer, Result>(new AddManufacturer(Guid.NewGuid(), new UserContext("test", Guid.Empty), manufacturer));

            var rack1Specification = new RackSpecification(Guid.NewGuid(), "ETSI2", "ETSI2")
            {
                Description = "ETSI Rack",
            };

            var rack2Specification = new RackSpecification(Guid.NewGuid(), "ETSI2", "ETSI2")
            {
                Description = "ETSI Rack",
            };


            // Act
            var addFirstRackSpecificationCommandResult = await _commandDispatcher.HandleAsync<AddRackSpecification, Result>(new AddRackSpecification(Guid.NewGuid(), new UserContext("test", Guid.Empty), rack1Specification));
            var addSecondRackSpecificationCommandResult = await _commandDispatcher.HandleAsync<AddRackSpecification, Result>(new AddRackSpecification(Guid.NewGuid(), new UserContext("test", Guid.Empty), rack2Specification));

            // Assert
            addFirstRackSpecificationCommandResult.IsSuccess.Should().BeTrue();
            addSecondRackSpecificationCommandResult.IsSuccess.Should().BeFalse();
        }
    }
}
