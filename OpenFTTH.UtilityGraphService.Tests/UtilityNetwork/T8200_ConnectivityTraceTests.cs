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
    [Order(8200)]
    public class T8200_ConnectivityTraceTests
    {
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;

        public T8200_ConnectivityTraceTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }

        [Fact, Order(1)]
        public async Task CableConnectivityTraceInCO1_ShouldSucceed()
        {
            // Setup
            var sutRouteNodeId = TestRouteNetwork.CO_1;
            var sutCableName = "K69373563";

            var cable = FindSpanEquipmentRelatedToRouteNetworkElementByName(sutRouteNodeId, sutCableName);

            // Get connectivity trace on fiber 12
            var connectivityTraceQuery = new GetConnectivityTraceView(sutRouteNodeId, cable.SpanStructures[12].SpanSegments[0].Id);

            var connectivityTraceQueryResult = await _queryDispatcher.HandleAsync<GetConnectivityTraceView, Result<ConnectivityTraceView>>(
                connectivityTraceQuery
            );

            var hops = connectivityTraceQueryResult.Value.Hops;

            hops.Length.Should().Be(5);
            hops[0].HopSeqNo.Should().Be(1);
            hops[1].HopSeqNo.Should().Be(2);

            // check that segment ids and geometry is returned
            hops[2].RouteSegmentGeometries.Length.Should().Be(3);
            hops[2].RouteSegmentIds.Length.Should().Be(3);
            hops[2].TotalLength.Should().BeGreaterThan(1);

            // Check 1:32 splitter line
            hops[1].TerminalStructure.Should().Be("Splitter 1");
            hops[1].Terminal.Should().Be("ud5");


        }


        [Fact, Order(0)]
        public async Task TerminalEquipmentConnectivityTraceInCO1RackEquipment_ShouldSucceed()
        {
            // Setup
            var sutRouteNodeId = TestRouteNetwork.CO_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get first lisa tray in rack
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[0].SubrackMounts.First().TerminalEquipmentId, out var terminalEquipment);


            // Get connectivity trace
            var connectivityTraceQuery = new GetConnectivityTraceView(sutRouteNodeId, terminalEquipment.TerminalStructures[0].Terminals[0].Id);

            var connectivityTraceQueryResult = await _queryDispatcher.HandleAsync<GetConnectivityTraceView, Result<ConnectivityTraceView>>(
                connectivityTraceQuery
            );

            var hops = connectivityTraceQueryResult.Value.Hops;

            hops.Length.Should().Be(5);
            hops[0].HopSeqNo.Should().Be(1);
            hops[1].HopSeqNo.Should().Be(2);

        }


        [Fact, Order(3)]
        public async Task TerminalEquipmentConnectivityTraceInCC1_ShouldSucceed()
        {
            // Setup
            var sutRouteNodeId = TestRouteNetwork.CC_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CC_1;

            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get equipment
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.TerminalEquipmentReferences.First(), out var terminalEquipment);


            // Get connectivity trace
            var connectivityTraceQuery = new GetConnectivityTraceView(sutRouteNodeId, terminalEquipment.TerminalStructures[0].Terminals[0].Id);

            var connectivityTraceQueryResult = await _queryDispatcher.HandleAsync<GetConnectivityTraceView, Result<ConnectivityTraceView>> (
                connectivityTraceQuery
            );

            var hops = connectivityTraceQueryResult.Value.Hops;
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
