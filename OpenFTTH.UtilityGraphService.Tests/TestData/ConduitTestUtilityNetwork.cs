using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.TestData;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using System;
using System.Linq;
using System.Threading;

namespace OpenFTTH.TestData
{
    public class ConduitTestUtilityNetwork
    {
        private static bool _conduitsCreated = false;
        private static object _myLock = new object();

        private readonly IEventStore _eventStore;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;


        public static Guid N1 = Guid.Parse("ee5d0305-4229-41ef-a3a1-d21ba2a57932");
        public static Guid N2 = Guid.Parse("1336cc31-8372-4751-b0e2-44d35bb1fc9d");
        public static Guid N3 = Guid.Parse("20d2eb27-13bb-4867-8dd8-c3e606e61dc6");
        public static Guid N4 = Guid.Parse("288548b4-0384-4270-8f07-9bc09ed09020");
        public static Guid N5 = Guid.Parse("6d534ad4-bfe2-49fc-89b7-2e5007fccc73");
        public static Guid N6 = Guid.Parse("8f4af9a4-9724-4f1f-8c8b-9bc12fdd62dd");
        public static Guid N7 = Guid.Parse("6c12b508-c48d-4012-a911-ac15d432fdc0");
        public static Guid N8 = Guid.Parse("5647b77c-02e4-4e0b-9895-ef567701f7c1");

        public static Guid S1 = Guid.Parse("8da3d93b-5f53-4750-a7a4-91dcf310f1e8");
        public static Guid S2 = Guid.Parse("02aa3ea1-e9fc-4380-b2ac-90c2c7bc4d23");
        public static Guid S3 = Guid.Parse("aecbe282-82a0-4501-b6fc-20d8d2224ea2");
        public static Guid S4 = Guid.Parse("443e8a0f-f184-441e-9b56-6906a8fb29f4");
        public static Guid S5 = Guid.Parse("adb58638-fbb3-4a30-b188-641b040e9b98");
        public static Guid S6 = Guid.Parse("c628b69c-c1e2-484a-8a17-b2c7ab4e9b54");
        public static Guid S7 = Guid.Parse("133a8f25-d1a4-445a-a245-e4461f2c03ae");
        public static Guid S8 = Guid.Parse("ca43fba4-02bc-4fef-9077-545831e195bb");
        public static Guid S9 = Guid.Parse("7613964c-d769-4ceb-a7aa-49ff39bcf762");
        public static Guid S10 = Guid.Parse("5b7f1870-71d4-4168-87ff-8cf0bc795aef");

        public static Guid NodeContainer_N1;
        public static Guid NodeContainer_N2;
        public static Guid NodeContainer_N3;
        public static Guid NodeContainer_N4;


        public static Guid Conduit_N1_N2_1;
        public static Guid Conduit_N1_N2_2;
        public static Guid Conduit_N1_N2_3;
        public static Guid Conduit_N1_N2_4;
        public static Guid Conduit_N2_N1_1;
        public static Guid Conduit_N4_N1_1;

        public static Guid Conduit_N2_N3_1;
        public static Guid Conduit_N2_N3_2;
        public static Guid Conduit_N2_N4_1;
        public static Guid Conduit_N4_N2_1;
        public static Guid Conduit_N2_N4_2;

        public static Guid Conduit_N3_N4_1;
        public static Guid Conduit_N3_N4_2;

        public static Guid Conduit_3x10_N1_N3;
        public static Guid Conduit_Single_N3_N7;


        public ConduitTestUtilityNetwork(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
            _ = new TestSpecifications(commandDispatcher, queryDispatcher);
        }

       

        public ConduitTestUtilityNetwork Run()
        {
            if (_conduitsCreated)
                return this;

            lock (_myLock)
            {
                // double-checked locking
                if (_conduitsCreated)
                    return this;

                // Place some conduits in the route network we can play with

                Conduit_N1_N2_1 = PlaceConduit(TestSpecifications.Flex_Ø40_Red, new RouteNetworkElementIdList() { S1 }, null, "D_N1_N2_1");
                Conduit_N1_N2_2 = PlaceConduit(TestSpecifications.Flex_Ø40_Red, new RouteNetworkElementIdList() { S1 }, null, "D_N1_N2_2");
                Conduit_N1_N2_3 = PlaceConduit(TestSpecifications.Flex_Ø40_Red, new RouteNetworkElementIdList() { S1 }, null, "D_N1_N2_3");
                Conduit_N1_N2_4 = PlaceConduit(TestSpecifications.Flex_Ø40_Red, new RouteNetworkElementIdList() { S1 }, null, "D_N1_N2_4");
                Conduit_N2_N1_1 = PlaceConduit(TestSpecifications.Flex_Ø40_Red, new RouteNetworkElementIdList() { S8, S4, S7 }, null, "D_N2_N1_1");
                Conduit_N4_N1_1 = PlaceConduit(TestSpecifications.Flex_Ø40_Red, new RouteNetworkElementIdList() { S10, S6, S5, S4, S7 }, null, "D_N4_N1_1");

                Conduit_N2_N3_1 = PlaceConduit(TestSpecifications.Flex_Ø40_Red, new RouteNetworkElementIdList() { S2 }, null, "D_N2_N3_1");
                Conduit_N2_N3_2 = PlaceConduit(TestSpecifications.Flex_Ø40_Red, new RouteNetworkElementIdList() { S2 }, null, "D_N2_N3_2");
                Conduit_N2_N4_1 = PlaceConduit(TestSpecifications.Flex_Ø40_Red, new RouteNetworkElementIdList() { S2, S3 }, null, "D_N2_N4_1");
                Conduit_N4_N2_1 = PlaceConduit(TestSpecifications.Flex_Ø40_Red, new RouteNetworkElementIdList() { S3, S2 }, null, "D_N4_N2_1");
                Conduit_N2_N4_2 = PlaceConduit(TestSpecifications.Flex_Ø40_Red, new RouteNetworkElementIdList() { S8, S5, S6, S10 }, null, "D_N2_N4_2");

                Conduit_N3_N4_1 = PlaceConduit(TestSpecifications.Flex_Ø40_Red, new RouteNetworkElementIdList() { S3 }, null, "D_N3_N4_1");
                Conduit_N3_N4_2 = PlaceConduit(TestSpecifications.Flex_Ø40_Red, new RouteNetworkElementIdList() { S3 }, null, "D_N3_N4_2");

                Conduit_3x10_N1_N3 = PlaceConduit(TestSpecifications.Multi_Ø32_3x10, new RouteNetworkElementIdList() { S1, S2 }, null, "3x10");
                Conduit_Single_N3_N7 = PlaceConduit(TestSpecifications.CustomerConduit_Ø12_Orange, new RouteNetworkElementIdList() { S3, S10 }, null, "CustomerConduit");


                // Place node containers
                NodeContainer_N1 = PlaceNodeContainer(TestSpecifications.Well_Cubis_STAKKAbox_MODULA_900x450, TestSpecifications.Manu_Fiberpowertech, N1);
                NodeContainer_N2 = PlaceNodeContainer(TestSpecifications.Well_Cubis_STAKKAbox_MODULA_900x450, TestSpecifications.Manu_Fiberpowertech, N2);
                NodeContainer_N3 = PlaceNodeContainer(TestSpecifications.Well_Cubis_STAKKAbox_MODULA_900x450, TestSpecifications.Manu_Fiberpowertech, N3);
                NodeContainer_N4 = PlaceNodeContainer(TestSpecifications.Well_Cubis_STAKKAbox_MODULA_900x450, TestSpecifications.Manu_Fiberpowertech, N4);

                // Affix conduits in N2
                AffixSpanEquipmentToContainer(Conduit_N1_N2_1, NodeContainer_N2, NodeContainerSideEnum.West);
                AffixSpanEquipmentToContainer(Conduit_N1_N2_2, NodeContainer_N2, NodeContainerSideEnum.West);
                AffixSpanEquipmentToContainer(Conduit_N1_N2_3, NodeContainer_N2, NodeContainerSideEnum.West);
                AffixSpanEquipmentToContainer(Conduit_N2_N1_1, NodeContainer_N2, NodeContainerSideEnum.South);

                AffixSpanEquipmentToContainer(Conduit_N2_N3_1, NodeContainer_N2, NodeContainerSideEnum.East);
                AffixSpanEquipmentToContainer(Conduit_N2_N3_2, NodeContainer_N2, NodeContainerSideEnum.East);
                AffixSpanEquipmentToContainer(Conduit_N2_N4_1, NodeContainer_N2, NodeContainerSideEnum.East);
                AffixSpanEquipmentToContainer(Conduit_N4_N2_1, NodeContainer_N2, NodeContainerSideEnum.East);
                AffixSpanEquipmentToContainer(Conduit_N2_N4_2, NodeContainer_N2, NodeContainerSideEnum.South);

                // Affix conduits in N3
                //AffixSpanEquipmentToContainer(Conduit_N2_N3_1, NodeContainer_N3, NodeContainerSideEnum.West);
                AffixSpanEquipmentToContainer(Conduit_N2_N3_2, NodeContainer_N3, NodeContainerSideEnum.West);
                AffixSpanEquipmentToContainer(Conduit_N3_N4_1, NodeContainer_N3, NodeContainerSideEnum.East);
                AffixSpanEquipmentToContainer(Conduit_N3_N4_2, NodeContainer_N3, NodeContainerSideEnum.East);
                AffixSpanEquipmentToContainer(Conduit_3x10_N1_N3, NodeContainer_N3, NodeContainerSideEnum.West);
                AffixSpanEquipmentToContainer(Conduit_Single_N3_N7, NodeContainer_N3, NodeContainerSideEnum.East);


                // Connect conduits
                ConnectSingleConduitInNode(N2, Conduit_N1_N2_2, Conduit_N2_N3_2);
                ConnectSingleConduitInNode(N3, Conduit_N2_N3_2, Conduit_N3_N4_2);

                Thread.Sleep(100);

                _conduitsCreated = true;
            }

            return this;
        }

        private Guid PlaceConduit(Guid specificationId, RouteNetworkElementIdList walkIds, AddressInfo? addressInfo = null, string? name = null)
        {
            // Register walk of interest
            var walkOfInterestId = Guid.NewGuid();
            var registerWalkOfInterestCommand = new RegisterWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), walkOfInterestId, walkIds);
            var registerWalkOfInterestCommandResult = _commandDispatcher.HandleAsync<RegisterWalkOfInterest, Result<RouteNetworkInterest>>(registerWalkOfInterestCommand).Result;

            // Place conduit
            var placeSpanEquipmentCommand = new PlaceSpanEquipmentInRouteNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty), Guid.NewGuid(), specificationId, registerWalkOfInterestCommandResult.Value)
            {
                AddressInfo = addressInfo,
                NamingInfo = name == null ? null : new NamingInfo(name, null)
            };

            var placeSpanEquipmentResult = _commandDispatcher.HandleAsync<PlaceSpanEquipmentInRouteNetwork, Result>(placeSpanEquipmentCommand).Result;

            if (placeSpanEquipmentResult.IsFailed)
                throw new ApplicationException(placeSpanEquipmentResult.Errors.First().Message);

            return placeSpanEquipmentCommand.SpanEquipmentId;
        }

        private Guid PlaceNodeContainer(Guid specificationId, Guid manufacturerId, Guid routeNodeId)
        {
            var nodeOfInterestId = Guid.NewGuid();
            var registerNodeOfInterestCommand = new RegisterNodeOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), nodeOfInterestId, routeNodeId);
            var registerNodeOfInterestCommandResult = _commandDispatcher.HandleAsync<RegisterNodeOfInterest, Result<RouteNetworkInterest>>(registerNodeOfInterestCommand).Result;

            var placeNodeContainerCommand = new PlaceNodeContainerInRouteNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty), Guid.NewGuid(), specificationId, registerNodeOfInterestCommandResult.Value)
            {
                ManufacturerId = manufacturerId
            };

            var placeNodeContainerResult = _commandDispatcher.HandleAsync<PlaceNodeContainerInRouteNetwork, Result>(placeNodeContainerCommand).Result;

            if (placeNodeContainerResult.IsFailed)
                throw new ApplicationException(placeNodeContainerResult.Errors.First().Message);

            return placeNodeContainerCommand.NodeContainerId;
        }

        private void AffixSpanEquipmentToContainer(Guid spanEquipmentId, Guid nodeContainerId, NodeContainerSideEnum side)
        {
            var affixConduitToContainerCommand = new AffixSpanEquipmentToNodeContainer(Guid.NewGuid(), new UserContext("test", Guid.Empty),
               spanEquipmentOrSegmentId: spanEquipmentId,
               nodeContainerId: nodeContainerId,
               nodeContainerIngoingSide: side
           );

            var affixResult = _commandDispatcher.HandleAsync<AffixSpanEquipmentToNodeContainer, Result>(affixConduitToContainerCommand).Result;

            if (affixResult.IsFailed)
                throw new ApplicationException(affixResult.Errors.First().Message);
        }

        public void ConnectSingleConduitInNode(Guid routeNodeId, Guid conduit1Id, Guid conduit2Id)
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
            utilityNetwork.TryGetEquipment<SpanEquipment>(conduit1Id, out var sutFromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(conduit2Id, out var sutToSpanEquipment);

            // Connect outer conduit to outer conduit
            var connectCmd = new ConnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: routeNodeId,
                spanSegmentsToConnect: new Guid[] {
                    sutFromSpanEquipment.SpanStructures[0].SpanSegments[0].Id,
                    sutToSpanEquipment.SpanStructures[0].SpanSegments[0].Id
                }
            );

            var connectResult = _commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd).Result;

            if (connectResult.IsFailed)
                throw new ApplicationException(connectResult.Errors.First().Message);
        }

        public SpanEquipment PlaceCableDirectlyInRouteNetwork(string cableName, Guid cableSpecId, Guid[] routeNetworkSegmentIds)
        {
            var routingHops = new RoutingHop[]
            {
                new RoutingHop(routeNetworkSegmentIds)
            };

            var placeSpanEquipmentCommand = new PlaceSpanEquipmentInUtilityNetwork(Guid.NewGuid(), new UserContext("test", Guid.Empty), Guid.NewGuid(), cableSpecId, routingHops)
            {
                NamingInfo = new NamingInfo(cableName, null)
            };

            // Act
            var placeSpanEquipmentResult = _commandDispatcher.HandleAsync<PlaceSpanEquipmentInUtilityNetwork, Result>(placeSpanEquipmentCommand).Result;

            if (placeSpanEquipmentResult.IsFailed)
                throw new ApplicationException(placeSpanEquipmentResult.Errors.First().Message);


            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
            utilityNetwork.TryGetEquipment<SpanEquipment>(placeSpanEquipmentCommand.SpanEquipmentId, out var spanEquipment);

            return spanEquipment;
        }

        public RouteNetworkElementIdList GetWalkOfInterest(Guid interestId)
        {
            var routeNetworkQueryResult = _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
                 new GetRouteNetworkDetails(new InterestIdList() { interestId })
                 {
                     RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementAndInterestObjects
                 }
            ).Result;

            if (routeNetworkQueryResult.IsFailed)
                throw new ApplicationException(routeNetworkQueryResult.Errors.First().Message);

            return routeNetworkQueryResult.Value.Interests.First().RouteNetworkElementRefs;
        }

        public SpanEquipment AffixCableToSingleConduit(Guid routeNodeId, Guid cableId, Guid conduitId)
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            ///utilityNetwork.TryGetEquipment<SpanEquipment>(cableId, out var cableBeforeAffix);

            utilityNetwork.TryGetEquipment<SpanEquipment>(conduitId, out var conduit);

            var affixtCommand = new AffixSpanEquipmentToParent(Guid.NewGuid(), new UserContext("test", Guid.Empty), routeNodeId, cableId, conduit.SpanStructures[0].SpanSegments[0].Id);

            var affixCommandResult = _commandDispatcher.HandleAsync<AffixSpanEquipmentToParent, Result>(affixtCommand).Result;

            if (affixCommandResult.IsFailed)
                throw new ApplicationException(affixCommandResult.Errors.First().Message);
                       
            utilityNetwork.TryGetEquipment<SpanEquipment>(cableId, out var cableAfterAffix);

            return cableAfterAffix;
        }
    }
}

