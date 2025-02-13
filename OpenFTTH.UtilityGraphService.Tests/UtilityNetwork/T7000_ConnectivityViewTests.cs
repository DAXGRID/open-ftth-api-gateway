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
    [Order(7000)]
    public class T7000_ConnectivityViewTests
    {
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;

        public T7000_ConnectivityViewTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }


        [Fact, Order(1)]
        public async Task GetSpanEquipmentConnectivityViewOnCable_ShouldSucceed()
        {
            var sutRouteNetworkElementId = TestRouteNetwork.CC_1;

            var cable = FindSpanEquipmentRelatedToRouteNetworkElementByName(sutRouteNetworkElementId, "K666");

            var connectivityTrace = new GetSpanEquipmentConnectivityView(sutRouteNetworkElementId, new Guid[] { cable.Id });

            // Act
            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetSpanEquipmentConnectivityView, Result<SpanEquipmentAZConnectivityViewModel>>(
                connectivityTrace
            );

            // Assert
            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;
            var firstSpanEquipment = viewModel.SpanEquipments.First();

            firstSpanEquipment.Lines[0].Name.Should().Be("Tube 1 Fiber 1");
            firstSpanEquipment.Lines[11].Name.Should().Be("Tube 1 Fiber 12");
            firstSpanEquipment.Lines[12].Name.Should().Be("Tube 2 Fiber 1");
        }

        [Fact, Order(2)]
        public async Task GetSpanEquipmentConnectivityViewOnConduit_ShouldSucceed()
        {
            var sutRouteNetworkElementId = TestRouteNetwork.CC_1;

            var conduit = FindSpanEquipmentRelatedToRouteNetworkElementById(sutRouteNetworkElementId, TestUtilityNetwork.MultiConduit_10x10_HH_1_to_HH_10);

            var connectivityTrace = new GetSpanEquipmentConnectivityView(sutRouteNetworkElementId, new Guid[] { conduit.Id });

            // Act
            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetSpanEquipmentConnectivityView, Result<SpanEquipmentAZConnectivityViewModel>>(
                connectivityTrace
            );

            // Assert
            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;
        }


        [Fact, Order(3)]
        public async Task GetSpanEquipmentConnectivityViewOnCableK69373563_ShouldSucceed()
        {
            var sutRouteNetworkElementId = TestRouteNetwork.CO_1;

            var cable = FindSpanEquipmentRelatedToRouteNetworkElementByName(sutRouteNetworkElementId, "K69373563");

            var connectivityTrace = new GetSpanEquipmentConnectivityView(sutRouteNetworkElementId, new Guid[] { cable.Id });

            // Act
            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetSpanEquipmentConnectivityView, Result<SpanEquipmentAZConnectivityViewModel>>(
                connectivityTrace
            );

            // Assert
            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;

            var fiber2Line = viewModel.SpanEquipments.First().Lines[1];
        }


        [Fact, Order(4)]
        public async Task GetTerminalEquipmentConnectivityViewOnCO1_ShouldSucceed()
        {

            // Get faces
            var connectivityFaceQuery = new GetConnectivityFaces(TestRouteNetwork.CO_1);

            var connectivityFaceQueryResult = await _queryDispatcher.HandleAsync<GetConnectivityFaces, Result<List<ConnectivityFace>>>(
                connectivityFaceQuery
            );

            connectivityFaceQueryResult.IsSuccess.Should().BeTrue();

            var connectivityFaces = connectivityFaceQueryResult.Value;

            connectivityFaces.Count(f => f.EquipmentKind == ConnectivityEquipmentKindEnum.TerminalEquipment).Should().BeGreaterThan(0);
            connectivityFaces.Count(f => f.EquipmentKind == ConnectivityEquipmentKindEnum.SpanEquipment).Should().BeGreaterThan(0);

            var terminalEquipmentFace = connectivityFaces.First(f => f.EquipmentKind == ConnectivityEquipmentKindEnum.TerminalEquipment);

            var connectivityTrace = new GetTerminalEquipmentConnectivityView(TestRouteNetwork.CO_1, terminalEquipmentFace.EquipmentId);

            // Act
            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetTerminalEquipmentConnectivityView, Result<TerminalEquipmentAZConnectivityViewModel>>(
                connectivityTrace
            );

            // Assert
            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;
        }

        [Fact, Order(4)]
        public async Task GetOneTrayRackTerminalEquipmentConnectivityViewInCO1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CO_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CO_1;

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get equipment
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.Racks[0].SubrackMounts.First().TerminalEquipmentId, out var terminalEquipment);

            var connectivityTrace = new GetTerminalEquipmentConnectivityView(sutNodeId, terminalEquipment.Id);

            // Act
            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetTerminalEquipmentConnectivityView, Result<TerminalEquipmentAZConnectivityViewModel>>(
                connectivityTrace
            );

            // Assert
            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value.TerminalEquipments.First();

            var test = viewModel.TerminalStructures[0].Lines[0];
            test.Z.End.Should().Be("C12345678-1 (Engum Møllevej 3)");


        }

        
        [Fact, Order(5)]
        public async Task GetTerminalEquipmentConnectivityViewOnCC1_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var sutNodeId = TestRouteNetwork.CC_1;
            var sutNodeContainerId = TestUtilityNetwork.NodeContainer_CC_1;

            // Get node container
            utilityNetwork.TryGetEquipment<NodeContainer>(sutNodeContainerId, out var nodeContainer);

            // Get equipment
            utilityNetwork.TryGetEquipment<TerminalEquipment>(nodeContainer.TerminalEquipmentReferences.First(), out var terminalEquipment);

            var connectivityTrace = new GetTerminalEquipmentConnectivityView(sutNodeId, terminalEquipment.Id);

            // Act
            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetTerminalEquipmentConnectivityView, Result<TerminalEquipmentAZConnectivityViewModel>>(
                connectivityTrace
            );

            // Assert
            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value.TerminalEquipments;

        }

        [Fact, Order(6)]
        public async Task K69373563ConnectivityViewTest_ShouldSucceed()
        {
            // Setup
            var sutRouteNodeId = TestRouteNetwork.CC_1;
            var sutCableName = "K69373563";

            var cable = FindSpanEquipmentRelatedToRouteNetworkElementByName(sutRouteNodeId, sutCableName);

            var connectivityTrace = new GetSpanEquipmentConnectivityView(sutRouteNodeId, new Guid[] { cable.Id });

            // Act
            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetSpanEquipmentConnectivityView, Result<SpanEquipmentAZConnectivityViewModel>>(
                connectivityTrace
            );

            // Assert
            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;

        }


        [Fact, Order(7)]
        public async Task GetSpanEquipmentConnectivityViewOnConduit5x10_CO1_HH1_ShouldSucceed()
        {
            var sutRouteNetworkElementId = TestRouteNetwork.CO_1;

            var conduit = FindSpanEquipmentRelatedToRouteNetworkElementByName(sutRouteNetworkElementId, "5x10_CO1_HH1");

            var connectivityTrace = new GetSpanEquipmentConnectivityView(sutRouteNetworkElementId, new Guid[] { conduit.Id });

            // Act
            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetSpanEquipmentConnectivityView, Result<SpanEquipmentAZConnectivityViewModel>>(
                connectivityTrace
            );

            // Assert
            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;

            var line = viewModel.SpanEquipments.First().Lines[1];
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
