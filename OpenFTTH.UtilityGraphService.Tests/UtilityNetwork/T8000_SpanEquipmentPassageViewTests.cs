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
    [Order(8000)]
    public class T8000_SpanEquipmentConnectivityViewTests
    {
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;

        public T8000_SpanEquipmentConnectivityViewTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;

            new TestSpecifications(_commandDispatcher, _queryDispatcher).Run();
            new TestUtilityNetwork(_commandDispatcher, _queryDispatcher).Run();
        }


        [Fact, Order(1)]
        public async Task GetSpanEquipmentPassageViewOnComplexCableWithManyHops_ShouldSucceed()
        {
            var sutRouteNetworkElementId = TestRouteNetwork.FP_2;

            var cable = FindSpanEquipmentRelatedToRouteNetworkElementByName(sutRouteNetworkElementId, "1u-1r-1u-1r");

            var connectivityTrace = new GetSpanEquipmentPassageView(sutRouteNetworkElementId, new Guid[] { cable.Id });

            // Act
            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetSpanEquipmentPassageView, Result<SpanEquipmentPassageViewModel>>(
                connectivityTrace
            );

            // Assert
            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;

            // Check from and to node names
            viewModel.SpanEquipments[0].Lines.Count().Should().Be(4);
            viewModel.SpanEquipments[0].Lines[0].From.Should().Be("CO-1");
            viewModel.SpanEquipments[0].Lines[0].To.Should().Be("HH-1");
            viewModel.SpanEquipments[0].Lines[1].From.Should().Be("HH-1");
            viewModel.SpanEquipments[0].Lines[1].To.Should().Be("HH-2");
            viewModel.SpanEquipments[0].Lines[2].From.Should().Be("HH-2");
            viewModel.SpanEquipments[0].Lines[2].To.Should().Be("FP-2");
            viewModel.SpanEquipments[0].Lines[3].From.Should().Be("FP-2");
            viewModel.SpanEquipments[0].Lines[3].To.Should().Be("HH-2");

            // Check segment ids and that segment geometry exists for each hop
            viewModel.SpanEquipments[0].Lines[0].RouteSegmentIds.Count().Should().Be(1);
            viewModel.SpanEquipments[0].Lines[0].RouteSegmentGeometries.Count().Should().Be(1);
            viewModel.SpanEquipments[0].Lines[0].RouteSegmentIds[0].Should().Be(TestRouteNetwork.S1);

            viewModel.SpanEquipments[0].Lines[1].RouteSegmentIds.Count().Should().Be(1);
            viewModel.SpanEquipments[0].Lines[1].RouteSegmentGeometries.Count().Should().Be(1);
            viewModel.SpanEquipments[0].Lines[1].RouteSegmentIds[0].Should().Be(TestRouteNetwork.S2);

            viewModel.SpanEquipments[0].Lines[2].RouteSegmentIds.Count().Should().Be(1);
            viewModel.SpanEquipments[0].Lines[2].RouteSegmentGeometries.Count().Should().Be(1);
            viewModel.SpanEquipments[0].Lines[2].RouteSegmentIds[0].Should().Be(TestRouteNetwork.S3);

            viewModel.SpanEquipments[0].Lines[3].RouteSegmentIds.Count().Should().Be(1);
            viewModel.SpanEquipments[0].Lines[3].RouteSegmentGeometries.Count().Should().Be(1);
            viewModel.SpanEquipments[0].Lines[3].RouteSegmentIds[0].Should().Be(TestRouteNetwork.S3);

            // Check length
            double totalLength = 0;
            viewModel.SpanEquipments[0].Lines[0].SegmentLength.Should().BeGreaterThan(0);
            totalLength += viewModel.SpanEquipments[0].Lines[0].SegmentLength;
            viewModel.SpanEquipments[0].Lines[0].SegmentLength.Should().Be(totalLength);
            
            viewModel.SpanEquipments[0].Lines[1].SegmentLength.Should().BeGreaterThan(0);
            totalLength += viewModel.SpanEquipments[0].Lines[1].SegmentLength;
            viewModel.SpanEquipments[0].Lines[1].CumulativeDistance.Should().Be(totalLength);

            viewModel.SpanEquipments[0].Lines[2].SegmentLength.Should().BeGreaterThan(0);
            totalLength += viewModel.SpanEquipments[0].Lines[2].SegmentLength;
            viewModel.SpanEquipments[0].Lines[2].CumulativeDistance.Should().Be(totalLength);

            viewModel.SpanEquipments[0].Lines[3].SegmentLength.Should().BeGreaterThan(0);
            totalLength += viewModel.SpanEquipments[0].Lines[3].SegmentLength;
            viewModel.SpanEquipments[0].Lines[3].CumulativeDistance.Should().Be(totalLength);
        }


        [Fact, Order(2)]
        public async Task GetSpanEquipmentPassageViewOnCableK667_ShouldSucceed()
        {
            // NB: cable K667 goes through connected conduits
            var sutRouteNetworkElementId = TestRouteNetwork.CC_1;

            var cable = FindSpanEquipmentRelatedToRouteNetworkElementByName(sutRouteNetworkElementId, "K667");

            var connectivityTrace = new GetSpanEquipmentPassageView(sutRouteNetworkElementId, new Guid[] { cable.Id });

            // Act
            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetSpanEquipmentPassageView, Result<SpanEquipmentPassageViewModel>>(
                connectivityTrace
            );

            // Assert
            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;

            // Check from and to node names
            viewModel.SpanEquipments[0].Lines.Count().Should().Be(3);
            viewModel.SpanEquipments[0].Lines[0].From.Should().Be("CO-1");
            viewModel.SpanEquipments[0].Lines[0].To.Should().Be("HH-1");
            viewModel.SpanEquipments[0].Lines[1].From.Should().Be("HH-1");
            viewModel.SpanEquipments[0].Lines[1].To.Should().Be("CC-1");
            viewModel.SpanEquipments[0].Lines[2].From.Should().Be("CC-1");
            viewModel.SpanEquipments[0].Lines[2].To.Should().Be("SP-1");

            // Check segment ids and that segment geometry exists for each hop
            viewModel.SpanEquipments[0].Lines[0].RouteSegmentIds.Count().Should().Be(1);
            viewModel.SpanEquipments[0].Lines[0].RouteSegmentGeometries.Count().Should().Be(1);
            viewModel.SpanEquipments[0].Lines[0].RouteSegmentIds[0].Should().Be(TestRouteNetwork.S1);

            viewModel.SpanEquipments[0].Lines[1].RouteSegmentIds.Count().Should().Be(2);
            viewModel.SpanEquipments[0].Lines[1].RouteSegmentGeometries.Count().Should().Be(2);
            viewModel.SpanEquipments[0].Lines[1].RouteSegmentIds[0].Should().Be(TestRouteNetwork.S2);
            viewModel.SpanEquipments[0].Lines[1].RouteSegmentIds[1].Should().Be(TestRouteNetwork.S4);

            viewModel.SpanEquipments[0].Lines[2].RouteSegmentIds.Count().Should().Be(1);
            viewModel.SpanEquipments[0].Lines[2].RouteSegmentGeometries.Count().Should().Be(1);
            viewModel.SpanEquipments[0].Lines[2].RouteSegmentIds[0].Should().Be(TestRouteNetwork.S5);


            // Check length
            double totalLength = 0;
            viewModel.SpanEquipments[0].Lines[0].SegmentLength.Should().BeGreaterThan(0);
            totalLength += viewModel.SpanEquipments[0].Lines[0].SegmentLength;
            viewModel.SpanEquipments[0].Lines[0].SegmentLength.Should().Be(totalLength);

            viewModel.SpanEquipments[0].Lines[1].SegmentLength.Should().BeGreaterThan(0);
            totalLength += viewModel.SpanEquipments[0].Lines[1].SegmentLength;
            viewModel.SpanEquipments[0].Lines[1].CumulativeDistance.Should().Be(totalLength);

            viewModel.SpanEquipments[0].Lines[2].SegmentLength.Should().BeGreaterThan(0);
            totalLength += viewModel.SpanEquipments[0].Lines[2].SegmentLength;
            viewModel.SpanEquipments[0].Lines[2].CumulativeDistance.Should().Be(totalLength);
        }

        [Fact, Order(0)]
        public async Task GetSpanEquipmentPassageViewNonConnectedConduit_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var routeThroughSpanEquipmentId = TestUtilityNetwork.MultiConduit_10x10_HH_1_to_HH_10;

            utilityNetwork.TryGetEquipment<SpanEquipment>(routeThroughSpanEquipmentId, out var routeThoughSpanEquipment);

            var sutRouteNetworkElementId = TestRouteNetwork.CC_1;

            // Sub conduit 1
            var sutSpanSegmentId = routeThoughSpanEquipment.SpanStructures[1].SpanSegments[0].Id;

            var connectivityTrace = new GetSpanEquipmentPassageView(sutRouteNetworkElementId, new Guid[] { sutSpanSegmentId });

            // Act
            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetSpanEquipmentPassageView, Result<SpanEquipmentPassageViewModel>>(
                connectivityTrace
            );

            // Assert
            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;

            // Check from and to node names
            viewModel.SpanEquipments[0].Lines.Count().Should().Be(1);
            viewModel.SpanEquipments[0].Lines[0].From.Should().Be("HH-1");
            viewModel.SpanEquipments[0].Lines[0].To.Should().Be("HH-10");

            // Check length
            double totalLength = 0;
            viewModel.SpanEquipments[0].Lines[0].SegmentLength.Should().BeGreaterThan(0);
            totalLength += viewModel.SpanEquipments[0].Lines[0].SegmentLength;
            viewModel.SpanEquipments[0].Lines[0].SegmentLength.Should().Be(totalLength);
        }


        [Fact, Order(11)]
        public async Task GetSpanEquipmentPassageViewOnConnectedConduit_ShouldSucceed()
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var routeThroughSpanEquipmentId = TestUtilityNetwork.MultiConduit_5x10_CO_1_to_HH_1;

            utilityNetwork.TryGetEquipment<SpanEquipment>(routeThroughSpanEquipmentId, out var routeThoughSpanEquipment);

            var sutRouteNetworkElementId = TestRouteNetwork.CC_1;

            // Sub conduit 3
            var sutSpanSegmentId = routeThoughSpanEquipment.SpanStructures[3].SpanSegments[0].Id;

            var connectivityTrace = new GetSpanEquipmentPassageView(sutRouteNetworkElementId, new Guid[] { sutSpanSegmentId });

            // Act
            var connectivityQueryResult = await _queryDispatcher.HandleAsync<GetSpanEquipmentPassageView, Result<SpanEquipmentPassageViewModel>>(
                connectivityTrace
            );

            // Assert
            connectivityQueryResult.IsSuccess.Should().BeTrue();

            var viewModel = connectivityQueryResult.Value;

            // Check from and to node names
            viewModel.SpanEquipments[0].Lines.Count().Should().Be(3);
            viewModel.SpanEquipments[0].Lines[0].From.Should().Be("CO-1");
            viewModel.SpanEquipments[0].Lines[0].To.Should().Be("HH-1");
            viewModel.SpanEquipments[0].Lines[1].From.Should().Be("HH-1");
            viewModel.SpanEquipments[0].Lines[1].To.Should().Be("CC-1");
            viewModel.SpanEquipments[0].Lines[2].From.Should().Be("CC-1");
            viewModel.SpanEquipments[0].Lines[2].To.Should().Be("SP-1");

            // Check segment ids and that segment geometry exists for each hop
            viewModel.SpanEquipments[0].Lines[0].RouteSegmentIds.Count().Should().Be(1);
            viewModel.SpanEquipments[0].Lines[0].RouteSegmentGeometries.Count().Should().Be(1);
            viewModel.SpanEquipments[0].Lines[0].RouteSegmentIds[0].Should().Be(TestRouteNetwork.S1);

            viewModel.SpanEquipments[0].Lines[1].RouteSegmentIds.Count().Should().Be(2);
            viewModel.SpanEquipments[0].Lines[1].RouteSegmentGeometries.Count().Should().Be(2);
            viewModel.SpanEquipments[0].Lines[1].RouteSegmentIds[0].Should().Be(TestRouteNetwork.S2);
            viewModel.SpanEquipments[0].Lines[1].RouteSegmentIds[1].Should().Be(TestRouteNetwork.S4);

            viewModel.SpanEquipments[0].Lines[2].RouteSegmentIds.Count().Should().Be(1);
            viewModel.SpanEquipments[0].Lines[2].RouteSegmentGeometries.Count().Should().Be(1);
            viewModel.SpanEquipments[0].Lines[2].RouteSegmentIds[0].Should().Be(TestRouteNetwork.S5);


            // Check length
            double totalLength = 0;
            viewModel.SpanEquipments[0].Lines[0].SegmentLength.Should().BeGreaterThan(0);
            totalLength += viewModel.SpanEquipments[0].Lines[0].SegmentLength;
            viewModel.SpanEquipments[0].Lines[0].SegmentLength.Should().Be(totalLength);

            viewModel.SpanEquipments[0].Lines[1].SegmentLength.Should().BeGreaterThan(0);
            totalLength += viewModel.SpanEquipments[0].Lines[1].SegmentLength;
            viewModel.SpanEquipments[0].Lines[1].CumulativeDistance.Should().Be(totalLength);

            viewModel.SpanEquipments[0].Lines[2].SegmentLength.Should().BeGreaterThan(0);
            totalLength += viewModel.SpanEquipments[0].Lines[2].SegmentLength;
            viewModel.SpanEquipments[0].Lines[2].CumulativeDistance.Should().Be(totalLength);
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
