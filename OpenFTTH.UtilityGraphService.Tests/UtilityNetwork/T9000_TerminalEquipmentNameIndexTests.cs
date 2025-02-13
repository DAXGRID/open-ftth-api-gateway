using FluentAssertions;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.EventSourcing;
using OpenFTTH.TestData;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.Business.Graph;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions.Ordering;

namespace OpenFTTH.UtilityGraphService.Tests.UtilityNetwork
{
    [Order(9000)]
    public class T9000_TerminalEquipmentNameIndexTests
    {
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;
        private ConduitTestUtilityNetwork _conduitTestUtilityNetwork;

        public T9000_TerminalEquipmentNameIndexTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            _conduitTestUtilityNetwork = new ConduitTestUtilityNetwork(_eventStore, _commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public async Task SearchForNonExistingStuff()
        {
            var getEquipmentQuery = new GetEquipmentDetails("hej");

            var getEquipmentResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(getEquipmentQuery);

            getEquipmentResult.IsSuccess.Should().BeTrue();

            getEquipmentResult.Value.TerminalEquipment.Should().BeEmpty();
        }

        [Fact, Order(2)]
        public async Task TestLowerCaseSearch()
        {
            var getEquipmentQuery = new GetEquipmentDetails("olt 1");

            var getEquipmentResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(getEquipmentQuery);

            getEquipmentResult.IsSuccess.Should().BeTrue();

            getEquipmentResult.Value.TerminalEquipment.Should().HaveCount(1);
        }


        [Fact, Order(3)]
        public async Task TestSearchAfterNamingInfoUpdate()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var eqIdToUpdate = utilityNetwork.TerminalEquipmentIdByName["olt 1"].First();

            var updateCmd = new UpdateTerminalEquipmentProperties(Guid.NewGuid(), new UserContext("test", Guid.Empty), terminalEquipmentId: eqIdToUpdate)
            {
                NamingInfo = new NamingInfo() { Name = "Olt 2", Description = null }
            };

            await _commandDispatcher.HandleAsync<UpdateTerminalEquipmentProperties, Result>(updateCmd);

            utilityNetwork.TerminalEquipmentIdByName.ContainsKey("olt 1").Should().BeFalse();

            utilityNetwork.TerminalEquipmentIdByName.ContainsKey("olt 2").Should().BeTrue();

            var getEquipmentQuery = new GetEquipmentDetails("olt 2");

            var getEquipmentResult = await _queryDispatcher.HandleAsync<GetEquipmentDetails, Result<GetEquipmentDetailsResult>>(getEquipmentQuery);

            getEquipmentResult.IsSuccess.Should().BeTrue();

            getEquipmentResult.Value.TerminalEquipment.Should().HaveCount(1);
        }


    }
}
