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
    [Order(200)]
    public class T0200_SpanStructuretSpecificationTests
    {
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;

        public T0200_SpanStructuretSpecificationTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
        }

        [Fact]
        public async Task AddSpanStructureSpecificationTest()
        {
            // Setup
            var spec1 = new SpanStructureSpecification(Guid.NewGuid(), "Conduit", "Ø12", "Red")
            {
                OuterDiameter = 12,
                InnerDiameter = 10
            };

            var spec2 = new SpanStructureSpecification(Guid.NewGuid(), "Conduit", "Ø50", "Red")
            {
                OuterDiameter = 50,
                InnerDiameter = 45
            };

            // Act
            var cmd1 = new AddSpanStructureSpecification(Guid.NewGuid(), new UserContext("test", Guid.Empty), spec1);
            Result cmd1Result = await _commandDispatcher.HandleAsync<AddSpanStructureSpecification, Result>(cmd1);

            var cmd2 = new AddSpanStructureSpecification(Guid.NewGuid(), new UserContext("test", Guid.Empty), spec2);
            Result cmd2Result = await _commandDispatcher.HandleAsync<AddSpanStructureSpecification, Result>(cmd2);

            var spanStructureSpecificationsQueryResult = await _queryDispatcher.HandleAsync<GetSpanStructureSpecifications, Result<LookupCollection<SpanStructureSpecification>>>(new GetSpanStructureSpecifications());

            // Assert
            cmd1Result.IsSuccess.Should().BeTrue();
            cmd2Result.IsSuccess.Should().BeTrue();

            spanStructureSpecificationsQueryResult.IsSuccess.Should().BeTrue();
            spanStructureSpecificationsQueryResult.Value[spec1.Id].Should().BeEquivalentTo(spec1);
            spanStructureSpecificationsQueryResult.Value[spec2.Id].Should().BeEquivalentTo(spec2);
        }

        [Fact]
        public async Task DepecateSpanStructureSpecificationTest()
        {
            // Setup
            var spec1 = new SpanStructureSpecification(Guid.NewGuid(), "Conduit", "Ø12", "Blue")
            {
                OuterDiameter = 12,
                InnerDiameter = 10
            };

            var spec2 = new SpanStructureSpecification(Guid.NewGuid(), "Conduit", "Ø50", "Orange")
            {
                OuterDiameter = 50,
                InnerDiameter = 45
            };

            // Act
            await _commandDispatcher.HandleAsync<AddSpanStructureSpecification, Result>(new AddSpanStructureSpecification(Guid.NewGuid(), new UserContext("test", Guid.Empty), spec1));
            await _commandDispatcher.HandleAsync<AddSpanStructureSpecification, Result>(new AddSpanStructureSpecification(Guid.NewGuid(), new UserContext("test", Guid.Empty), spec2));
            await _commandDispatcher.HandleAsync<DeprecateSpanStructureSpecification, Result>(new DeprecateSpanStructureSpecification(Guid.NewGuid(), new UserContext("test", Guid.Empty), spec2.Id));

            var spanStructureSpecificationsQueryResult = await _queryDispatcher.HandleAsync<GetSpanStructureSpecifications, Result<LookupCollection<SpanStructureSpecification>>>(new GetSpanStructureSpecifications());

            // Assert
            spanStructureSpecificationsQueryResult.IsSuccess.Should().BeTrue();
            spanStructureSpecificationsQueryResult.Value[spec1.Id].Deprecated.Should().BeFalse();
            spanStructureSpecificationsQueryResult.Value[spec2.Id].Deprecated.Should().BeTrue();
        }
    }
}
