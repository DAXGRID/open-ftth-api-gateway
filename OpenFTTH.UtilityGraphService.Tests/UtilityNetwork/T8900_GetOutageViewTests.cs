using FluentAssertions;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.TestData;
using OpenFTTH.UtilityGraphService.API.Model.Outage;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.Business.Graph;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions.Ordering;

namespace OpenFTTH.UtilityGraphService.Tests.UtilityNetwork
{
    [Order(8900)]
    public class T8900_GetOutageViewTests
    {
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;
        private ConduitTestUtilityNetwork _conduitTestUtilityNetwork;

        public T8900_GetOutageViewTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            _conduitTestUtilityNetwork = new ConduitTestUtilityNetwork(_eventStore, _commandDispatcher, _queryDispatcher).Run();
        }


        [Fact, Order(2)]
        public async Task GetOutageViewOnS2()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutRouteNetworkElementId = TestRouteNetwork.S2;

            var getOutageViewQuery = new GetOutageView(sutRouteNetworkElementId);

            var getOutageViewResult = await _queryDispatcher.HandleAsync<GetOutageView, Result<OutageViewNode>>(getOutageViewQuery);


            getOutageViewResult.IsSuccess.Should().BeTrue();

            getOutageViewResult.Value.Nodes.Where(n => n.Description.Contains("1 {OutageInstallationsFound}")).Count().Should().Be(1);

            getOutageViewResult.Value.Should().NotBeNull();

            getOutageViewResult.Value.Nodes.Should().NotBeNull();


        }

        [Fact, Order(1)]
        public async Task GetOutageViewOn1_2Splitter_in_CO_1()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutRouteNetworkElementId = TestRouteNetwork.CO_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get 1:2 splitter that sit in pos 100
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[0].SubrackMounts.First(s => s.Position == 100).TerminalEquipmentId, out var split1_2);

            var getOutageViewQuery = new GetOutageView(sutRouteNetworkElementId, split1_2.Id);

            var getOutageViewResult = await _queryDispatcher.HandleAsync<GetOutageView, Result<OutageViewNode>>(getOutageViewQuery);


            getOutageViewResult.IsSuccess.Should().BeTrue();

            getOutageViewResult.Value.Should().NotBeNull();

            getOutageViewResult.Value.Nodes.Should().NotBeNull();

            getOutageViewResult.Value.Nodes[0].Nodes[0].Nodes[0].Nodes[0].Label.Should().Be("C12345678");
        }

        [Fact, Order(3)]
        public async Task GetOutageViewOnLisaInCO_1()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutRouteNetworkElementId = TestRouteNetwork.CO_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get first lisa tray in rack
            var firstLisaEqId = nodeContainer.Racks[0].SubrackMounts.First().TerminalEquipmentId;

            var getOutageViewQuery = new GetOutageView(sutRouteNetworkElementId, firstLisaEqId);

            var getOutageViewResult = await _queryDispatcher.HandleAsync<GetOutageView, Result<OutageViewNode>>(getOutageViewQuery);


            getOutageViewResult.IsSuccess.Should().BeTrue();

            getOutageViewResult.Value.Should().NotBeNull();

            getOutageViewResult.Value.Nodes.Should().NotBeNull();
        }

        [Fact, Order(4)]
        public async Task GetOutageViewOnRack_in_CO_1()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutRouteNetworkElementId = TestRouteNetwork.CO_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            var getOutageViewQuery = new GetOutageView(sutRouteNetworkElementId, (nodeContainer.Racks[0].Id));

            var getOutageViewResult = await _queryDispatcher.HandleAsync<GetOutageView, Result<OutageViewNode>>(getOutageViewQuery);

            getOutageViewResult.IsSuccess.Should().BeTrue();

            getOutageViewResult.Value.Should().NotBeNull();

            getOutageViewResult.Value.Nodes.Should().NotBeNull();

            getOutageViewResult.Value.Nodes.Count.Should().BeGreaterThan(50);

            getOutageViewResult.Value.Nodes[2].Nodes[0].Nodes[0].Nodes[0].Label.Should().Be("FK123456");
        }
    }
}
