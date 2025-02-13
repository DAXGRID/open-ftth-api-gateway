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
    [Order(300)]
    public class T0300_SpanEquipmentSpecificationTests
    {
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;

        public T0300_SpanEquipmentSpecificationTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
        }

        [Fact]
        public async Task AddValidMultiLevelSpanEquipmentSpecification_ShouldSucceed()
        {
            // Create manufacturer
            var manufacturer = new Manufacturer(Guid.NewGuid(), "Conduit Manufacturer");
            await _commandDispatcher.HandleAsync<AddManufacturer, Result>(new AddManufacturer(Guid.NewGuid(), new UserContext("test", Guid.Empty), manufacturer));


            // Setup some span structure specifications to be used in the span equipment specification
            var outerConduitSpanStructureSpec1 = new SpanStructureSpecification(Guid.NewGuid(), "Conduit", "Ø50", "Orange")
            {
                OuterDiameter = 50,
                InnerDiameter = 45
            };
            await _commandDispatcher.HandleAsync<AddSpanStructureSpecification, Result>(new AddSpanStructureSpecification(Guid.NewGuid(), new UserContext("test", Guid.Empty), outerConduitSpanStructureSpec1));

            var innerConduitSpanStructureSpec1 = new SpanStructureSpecification(Guid.NewGuid(), "Conduit", "Ø12/10", "Red")
            {
                OuterDiameter = 12,
                InnerDiameter = 10
            };
            await _commandDispatcher.HandleAsync<AddSpanStructureSpecification, Result>(new AddSpanStructureSpecification(Guid.NewGuid(), new UserContext("test", Guid.Empty), innerConduitSpanStructureSpec1));

            var innerConduitSpanStructureSpec2 = new SpanStructureSpecification(Guid.NewGuid(), "Conduit", "Ø12/10", "Blue")
            {
                OuterDiameter = 12,
                InnerDiameter = 10
            };
            await _commandDispatcher.HandleAsync<AddSpanStructureSpecification, Result>(new AddSpanStructureSpecification(Guid.NewGuid(), new UserContext("test", Guid.Empty), innerConduitSpanStructureSpec2));
                       

            // Setup a span equipment specification with 2 levels
            var spanEquipmentSpecification = new SpanEquipmentSpecification(Guid.NewGuid(), "Conduit", "Ø50 2x12",
                new SpanStructureTemplate(outerConduitSpanStructureSpec1.Id, 1, 1,
                    new SpanStructureTemplate[] {
                        new SpanStructureTemplate(innerConduitSpanStructureSpec1.Id, 2, 1, Array.Empty<SpanStructureTemplate>()),
                        new SpanStructureTemplate(innerConduitSpanStructureSpec2.Id, 2, 2, Array.Empty<SpanStructureTemplate>())
                    }
                )
            )
            {
                Description = "Ø50 2x12/10",
                ManufacturerRefs = new Guid[] { manufacturer.Id }
            };

            // Act
            var addSpanEquipmentSpecificationCommandResult = await _commandDispatcher.HandleAsync<AddSpanEquipmentSpecification, Result>(new AddSpanEquipmentSpecification(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipmentSpecification));

            var spanEqipmentSpecificationsQueryResult = await _queryDispatcher.HandleAsync<GetSpanEquipmentSpecifications, Result<LookupCollection<SpanEquipmentSpecification>>>(new GetSpanEquipmentSpecifications());


            // Assert
            addSpanEquipmentSpecificationCommandResult.IsSuccess.Should().BeTrue();
            spanEqipmentSpecificationsQueryResult.IsSuccess.Should().BeTrue();

        }

        [Fact]
        public async Task AddInvalidSpanEquipmentSpecificationWithRootTemplateLevelDifferentFromOne_ShouldFail()
        {
            // Setup some span structure specifications to be used in the span equipment specification
            var outerConduitSpanStructureSpec1 = new SpanStructureSpecification(Guid.NewGuid(), "Conduit", "Ø50", "Orange")
            {
                OuterDiameter = 50,
                InnerDiameter = 45
            };
            await _commandDispatcher.HandleAsync<AddSpanStructureSpecification, Result>(new AddSpanStructureSpecification(Guid.NewGuid(), new UserContext("test", Guid.Empty), outerConduitSpanStructureSpec1));


            // Setup a span equipment specification with level 0 in root span template. 
            // Must fail, because we want the root template to always have level 1
            var spanEquipmentSpecification = new SpanEquipmentSpecification(Guid.NewGuid(), "Conduit", "Ø50 2x12",
                new SpanStructureTemplate(outerConduitSpanStructureSpec1.Id, 0, 1,
                    new SpanStructureTemplate[] {
                    }
                ));

            // Act
            var addSpanEquipmentSpecificationCommandResult = await _commandDispatcher.HandleAsync<AddSpanEquipmentSpecification, Result>(new AddSpanEquipmentSpecification(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipmentSpecification));

            // Assert
            addSpanEquipmentSpecificationCommandResult.IsFailed.Should().BeTrue();

        }

        [Fact]
        public async Task AddInvalidSpanEquipmentSpecificationWithWrongChildTemplateLevel_ShouldFail()
        {
            // Setup some span structure specifications to be used in the span equipment specification
            var outerConduitSpanStructureSpec1 = new SpanStructureSpecification(Guid.NewGuid(), "Conduit", "Ø50", "Orange")
            {
                OuterDiameter = 50,
                InnerDiameter = 45
            };
            await _commandDispatcher.HandleAsync<AddSpanStructureSpecification, Result>(new AddSpanStructureSpecification(Guid.NewGuid(), new UserContext("test", Guid.Empty), outerConduitSpanStructureSpec1));

            // Add span equipment specification with child templates as level 2 set to level 3
            var spanEquipmentSpecification = new SpanEquipmentSpecification(Guid.NewGuid(), "Conduit", "Ø50 2x12",
                new SpanStructureTemplate(outerConduitSpanStructureSpec1.Id, 1, 1,
                    new SpanStructureTemplate[] {
                        new SpanStructureTemplate(outerConduitSpanStructureSpec1.Id, 3, 1, Array.Empty<SpanStructureTemplate>())
                    }
                ));

            // Act
            var addSpanEquipmentSpecificationCommandResult = await _commandDispatcher.HandleAsync<AddSpanEquipmentSpecification, Result>(new AddSpanEquipmentSpecification(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipmentSpecification));

            // Assert
            addSpanEquipmentSpecificationCommandResult.IsFailed.Should().BeTrue();

        }

        [Fact]
        public async Task AddInvalidSpanEquipmentSpecificationWithNonUniqueLevelAndPosition_ShouldFail()
        {
            // Setup some span structure specifications to be used in the span equipment specification
            var outerConduitSpanStructureSpec1 = new SpanStructureSpecification(Guid.NewGuid(), "Conduit", "Ø50", "Orange")
            {
                OuterDiameter = 50,
                InnerDiameter = 45
            };
            await _commandDispatcher.HandleAsync<AddSpanStructureSpecification, Result>(new AddSpanStructureSpecification(Guid.NewGuid(), new UserContext("test", Guid.Empty), outerConduitSpanStructureSpec1));

            // Add span equipment specification with two child template having same level and position
            var spanEquipmentSpecification = new SpanEquipmentSpecification(Guid.NewGuid(), "Conduit", "Ø50 2x12",
                new SpanStructureTemplate(outerConduitSpanStructureSpec1.Id, 1, 1,
                    new SpanStructureTemplate[] {
                        new SpanStructureTemplate(outerConduitSpanStructureSpec1.Id, 2, 1, Array.Empty<SpanStructureTemplate>()),
                        new SpanStructureTemplate(outerConduitSpanStructureSpec1.Id, 2, 1, Array.Empty<SpanStructureTemplate>())
                    }
                ));

            // Act
            var addSpanEquipmentSpecificationCommandResult = await _commandDispatcher.HandleAsync<AddSpanEquipmentSpecification, Result>(new AddSpanEquipmentSpecification(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipmentSpecification));

            // Assert
            addSpanEquipmentSpecificationCommandResult.IsFailed.Should().BeTrue();

        }

        [Fact]
        public async Task AddInvalidSpanEquipmentSpecificationWithNonExistingStructureSpecification_ShouldFail()
        {
            // Setup
            var spanEquipmentIdThatDontExist = Guid.NewGuid();

            var spanEquipmentSpecification = new SpanEquipmentSpecification(Guid.NewGuid(), "Conduit", "Ø50 2x12",
                new SpanStructureTemplate(spanEquipmentIdThatDontExist, 1, 1, Array.Empty<SpanStructureTemplate>()
                ));

            // Act
            var addSpanEquipmentSpecificationCommandResult = await _commandDispatcher.HandleAsync<AddSpanEquipmentSpecification, Result>(new AddSpanEquipmentSpecification(Guid.NewGuid(), new UserContext("test", Guid.Empty), spanEquipmentSpecification));

            // Assert
            addSpanEquipmentSpecificationCommandResult.IsFailed.Should().BeTrue();

        }


    }
}
