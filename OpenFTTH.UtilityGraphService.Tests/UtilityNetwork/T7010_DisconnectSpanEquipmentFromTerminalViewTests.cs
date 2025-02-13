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
    [Order(7010)]
    public class T7010_DisconnectSpanEquipmentFromTerminalViewTests
    {
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;

        public T7010_DisconnectSpanEquipmentFromTerminalViewTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }


        [Fact, Order(1)]
        public async Task GetSpanEquipmentConnectivityViewOnK69373563InCC_1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CC_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CC_1;

            var sutCableName = "K69373563";
            var sutCable = FindSpanEquipmentRelatedToRouteNetworkElementByName(sutNodeId, sutCableName);


            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get equipment
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.TerminalEquipmentReferences.First(), out var terminalEquipment);

            var connectivityTrace = new GetTerminalEquipmentConnectivityView(sutNodeId, terminalEquipment.Id);

            // Act
            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetTerminalEquipmentConnectivityView, Result<TerminalEquipmentAZConnectivityViewModel>>(
                connectivityTrace
            );

            var firstConnectionToCentralOffice = connectivityQueryResult.Value.TerminalEquipments.First().TerminalStructures[0].Lines[0].A;

            var getDisconnectView = new GetDisconnectSpanEquipmentFromTerminalView(firstConnectionToCentralOffice.ConnectedToSpanSegmentId, firstConnectionToCentralOffice.Terminal.Id);

            var getDisconnectViewQueryResult = await _queryDispatcher.HandleAsync<GetDisconnectSpanEquipmentFromTerminalView, Result<DisconnectSpanEquipmentFromTerminalView>>(
                getDisconnectView
            );

            // Assert
            connectivityQueryResult.IsSuccess.Should().BeTrue();
            getDisconnectViewQueryResult.IsSuccess.Should().BeTrue();

            getDisconnectViewQueryResult.Value.SpanEquipmentName.Should().Be(sutCableName);

            var lines = getDisconnectViewQueryResult.Value.Lines;

            lines.Should().HaveCount(72);

            // fiber 1 must not be connected
            lines[0].SegmentId.Should().Be(sutCable.SpanStructures[1].SpanSegments[0].Id);
            lines[0].IsConnected.Should().BeFalse();

            // fiber 2 must be connected to terminal equipment tray 1 pin 1
            lines[1].SegmentId.Should().Be(sutCable.SpanStructures[2].SpanSegments[0].Id);
            lines[1].IsConnected.Should().BeTrue();
            lines[1].TerminalId.Should().Be(terminalEquipment.TerminalStructures[0].Terminals[0].Id);

            // fiber 3 must be connected to terminal equipment tray 1 pin 6
            lines[2].SegmentId.Should().Be(sutCable.SpanStructures[3].SpanSegments[0].Id);
            lines[2].IsConnected.Should().BeTrue();
            lines[2].TerminalId.Should().Be(terminalEquipment.TerminalStructures[0].Terminals[5].Id);

        }


        [Fact, Order(2)]
        public async Task GetSpanEquipmentConnectivityViewOnK69373563InCO_1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CO_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            var sutCableName = "K69373563";
            var sutCable = FindSpanEquipmentRelatedToRouteNetworkElementByName(sutNodeId, sutCableName);


            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get equipment
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[0].SubrackMounts.First().TerminalEquipmentId, out var terminalEquipment);

            var connectivityTrace = new GetTerminalEquipmentConnectivityView(sutNodeId, terminalEquipment.Id);

            // Act
            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetTerminalEquipmentConnectivityView, Result<TerminalEquipmentAZConnectivityViewModel>>(
                connectivityTrace
            );

            var firstConnectionToCentralOffice = connectivityQueryResult.Value.TerminalEquipments.First().TerminalStructures[0].Lines[0].Z;

            var getDisconnectView = new GetDisconnectSpanEquipmentFromTerminalView(firstConnectionToCentralOffice.ConnectedToSpanSegmentId, firstConnectionToCentralOffice.Terminal.Id);

            var getDisconnectViewQueryResult = await _queryDispatcher.HandleAsync<GetDisconnectSpanEquipmentFromTerminalView, Result<DisconnectSpanEquipmentFromTerminalView>>(
                getDisconnectView
            );

            // Assert
            connectivityQueryResult.IsSuccess.Should().BeTrue();
            getDisconnectViewQueryResult.IsSuccess.Should().BeTrue();

            getDisconnectViewQueryResult.Value.SpanEquipmentName.Should().Be(sutCableName);

            var lines = getDisconnectViewQueryResult.Value.Lines;

            lines.Should().HaveCount(72);

            // fiber 1 must not be connected
            lines[0].SegmentId.Should().Be(sutCable.SpanStructures[1].SpanSegments[0].Id);
            lines[0].IsConnected.Should().BeFalse();
            lines[1].IsConnected.Should().BeTrue();
            lines[2].IsConnected.Should().BeTrue();
            lines[3].IsConnected.Should().BeTrue();
            lines[4].IsConnected.Should().BeTrue();
            lines[5].IsConnected.Should().BeFalse();
            lines[11].IsConnected.Should().BeTrue();
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

        private SpanEquipment? FindSpanEquipmentRelatedToRouteNetworkElementById(Guid routeNetworkElementId, Guid spanEquipmentId)
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
                if (spanEquipment.Id == spanEquipmentId)
                    return spanEquipment;
            }

            return null;
        }
    }
}
