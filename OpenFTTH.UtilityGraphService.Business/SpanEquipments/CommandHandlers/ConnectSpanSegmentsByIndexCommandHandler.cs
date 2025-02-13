using DAX.EventProcessing;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Changes;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class ConnectSpanSegmentsByIndexCommandHandler : ICommandHandler<ConnectSpanSegmentsByIndexAtRouteNode, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly IExternalEventProducer _externalEventProducer;
        private readonly UtilityNetworkProjection _utilityNetwork;

        public ConnectSpanSegmentsByIndexCommandHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = externalEventProducer;
            _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
        }

        public Task<Result> HandleAsync(ConnectSpanSegmentsByIndexAtRouteNode command)
        {
            if (command.FromSpanEquipmentId == Guid.Empty)
                return Task.FromResult(Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(ConnectSpanSegmentsAtRouteNodeErrorCodes.INVALID_FROM_SPAN_EQUIPMENT_ID_CANNOT_BE_EMPTY, "Must provide a from and to span equipment id")));

            if (command.ToSpanEquipmentId == Guid.Empty)
                return Task.FromResult(Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(ConnectSpanSegmentsAtRouteNodeErrorCodes.INVALID_TO_SPAN_EQUIPMENT_ID_CANNOT_BE_EMPTY, "Must provide a from and to span equipment id")));

            if (command.NumberOfUnits < 1)
                return Task.FromResult(Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(ConnectSpanSegmentsAtRouteNodeErrorCodes.INVALID_NUMBER_OF_UNITS_MUST_BE_GREATER_THAN_ZERO, "Number of units must be greather than zero")));

            if (command.FromSpanEquipmentId == command.ToSpanEquipmentId)
                return Task.FromResult(Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(ConnectSpanSegmentsAtRouteNodeErrorCodes.EXPECTED_SPAN_SEGMENTS_FROM_TWO_SPAN_EQUIPMENT, "Two different span equipments must be provided in fromSpanEquipment and toSpanEquipment")));

            // Build supporting structure holding information on how span equipments are to be connected to each other
            var spanEquipmentsToConnectBuilderResult = BuildSpanEquipmentsToConnect(command);

            if (spanEquipmentsToConnectBuilderResult.IsFailed)
                return Task.FromResult(Result.Fail(spanEquipmentsToConnectBuilderResult.Errors.First()));

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            var spanEquipmentsToConnect = spanEquipmentsToConnectBuilderResult.Value;

            if (spanEquipmentsToConnect.Count == 2)
            {
                var firstSpanEquipment = spanEquipmentsToConnect.Values.First();
                var secondSpanEquipment = spanEquipmentsToConnect.Values.Last();

                // Check that number of span segments from each span equipment is the same
                if (firstSpanEquipment.Connects.Count != secondSpanEquipment.Connects.Count)
                {
                    return Task.FromResult(
                        Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(
                            ConnectSpanSegmentsAtRouteNodeErrorCodes.EXPECTED_SAME_NUMBER_OF_SPAN_SEGMENTS_BELONGING_TO_TWO_SPAN_EQUIPMENT,
                            $"Cannot connect the span segments specified because {firstSpanEquipment.Connects.Count} span segments are selected from span equipment: {firstSpanEquipment.SpanEquipment.Id} and {secondSpanEquipment.Connects.Count} span segments are selected from span equipment: {secondSpanEquipment.SpanEquipment.Id} The number of span segments selected in the two span equipments must the same!")
                        )
                    );
                }

                // Connect the individual spans using junctions/terminals
                var connectResult = ConnectSpanSegmentsFromTwoSpanEquipment(commandContext, command.RouteNodeId, firstSpanEquipment, secondSpanEquipment);

                return Task.FromResult(connectResult);
            }
            else
            {
                return Task.FromResult(
                   Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(
                       ConnectSpanSegmentsAtRouteNodeErrorCodes.EXPECTED_SPAN_SEGMENTS_FROM_TWO_SPAN_EQUIPMENT,
                       $"Got span segments belonging to {spanEquipmentsToConnect.Count} This command can only handle connecting span segments between two span equipments.")
                   )
               );
            }
        }




        private Result ConnectSpanSegmentsFromTwoSpanEquipment(CommandContext cmdContext, Guid routeNodeId, SpanEquipmentWithConnectsHolder firstSpanEquipment, SpanEquipmentWithConnectsHolder secondSpanEquipment)
        {
            var spanEquipmentSpecifications = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;

            // Create junction/terminal ids used to connect span segments
            for (int i = 0; i < firstSpanEquipment.Connects.Count; i++)
            {
                var junctionId = Guid.NewGuid();

                firstSpanEquipment.Connects[i].ConnectInfo.TerminalId = junctionId;
                firstSpanEquipment.Connects[i].ConnectInfo.ConnectionDirection = SpanSegmentToTerminalConnectionDirection.FromSpanSegmentToTerminal;

                secondSpanEquipment.Connects[i].ConnectInfo.TerminalId = junctionId;
                secondSpanEquipment.Connects[i].ConnectInfo.ConnectionDirection = SpanSegmentToTerminalConnectionDirection.FromTerminalToSpanSegment;
            }

            // Connect the first span equipment to terminals
            var firstSpanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(firstSpanEquipment.SpanEquipment.Id);

            var firstSpanEquipmentConnectResult = firstSpanEquipmentAR.ConnectConduitSpanSegmentsToSimpleTerminals(
                cmdContext: cmdContext,
                routeNodeId: routeNodeId,
                specification: spanEquipmentSpecifications[firstSpanEquipment.SpanEquipment.SpecificationId],
                connects: firstSpanEquipment.Connects.Select(c => c.ConnectInfo).ToArray()
            );

            if (firstSpanEquipmentConnectResult.IsFailed)
                return firstSpanEquipmentConnectResult;

            // Connect the second span equipment to terminals
            var secondSpanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(secondSpanEquipment.SpanEquipment.Id);

            var secondSpanEquipmentConnectResult = secondSpanEquipmentAR.ConnectConduitSpanSegmentsToSimpleTerminals(
                cmdContext: cmdContext,
                routeNodeId: routeNodeId,
                specification: spanEquipmentSpecifications[secondSpanEquipment.SpanEquipment.SpecificationId],
                connects: secondSpanEquipment.Connects.Select(c => c.ConnectInfo).ToArray()
            );

            if (secondSpanEquipmentConnectResult.IsFailed)
                return secondSpanEquipmentConnectResult;

            _eventStore.Aggregates.Store(firstSpanEquipmentAR);
            _eventStore.Aggregates.Store(secondSpanEquipmentAR);

            NotifyExternalServicesAboutConnectivityChange(firstSpanEquipment.SpanEquipment.Id, secondSpanEquipment.SpanEquipment.Id, routeNodeId, "EquipmentConnectivityModification.Connect");

            return Result.Ok();
        }


        private Result<Dictionary<Guid, SpanEquipmentWithConnectsHolder>> BuildSpanEquipmentsToConnect(ConnectSpanSegmentsByIndexAtRouteNode command)
        {
            Dictionary<Guid, SpanEquipmentWithConnectsHolder> spanEquipmentsToConnect = new Dictionary<Guid, SpanEquipmentWithConnectsHolder>();

            if (!_utilityNetwork.TryGetEquipment<SpanEquipment>(command.FromSpanEquipmentId, out var fromSpanEquipment))
                return Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(ConnectSpanSegmentsAtRouteNodeErrorCodes.FROM_SPAN_EQUIPMENT_NOT_FOUND, $"Cannot find any span equipment in the utility graph with id: {command.FromSpanEquipmentId}"));

            if (!_utilityNetwork.TryGetEquipment<SpanEquipment>(command.ToSpanEquipmentId, out var toSpanEquipment))
                return Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(ConnectSpanSegmentsAtRouteNodeErrorCodes.TO_SPAN_EQUIPMENT_NOT_FOUND, $"Cannot find any span equipment in the utility graph with id: {command.ToSpanEquipmentId}"));

            // Check if index out of bounds
            if (command.FromStructureIndex + command.NumberOfUnits > fromSpanEquipment.SpanStructures.Length)
                return Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(ConnectSpanSegmentsAtRouteNodeErrorCodes.FROM_STRUCTURE_INDEX_OUT_OF_BOUNDS, $"From index: {command.FromStructureIndex} + number of units: {command.NumberOfUnits} is greater than the number of structures: {fromSpanEquipment.SpanStructures.Length} of from span equipment with id: {command.FromSpanEquipmentId}"));

            if (command.ToStructureIndex + command.NumberOfUnits > toSpanEquipment.SpanStructures.Length)
                return Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(ConnectSpanSegmentsAtRouteNodeErrorCodes.TO_STRUCTURE_INDEX_OUT_OF_BOUNDS, $"To index: {command.ToStructureIndex} + number of units: {command.NumberOfUnits} is greater than the number of structures: {toSpanEquipment.SpanStructures.Length} of to span equipment with id: {command.ToSpanEquipmentId}"));

            // Get from span equipment connects
            var fromConnectsResult = GetConnects(fromSpanEquipment, command.RouteNodeId, command.FromStructureIndex, command.NumberOfUnits);

            if (fromConnectsResult.IsSuccess)
                spanEquipmentsToConnect.Add(fromSpanEquipment.Id, fromConnectsResult.Value);
            else
                return Result.Fail(fromConnectsResult.Errors.First());

            // Get to span equipment connects
            var toConnectsResult = GetConnects(toSpanEquipment, command.RouteNodeId, command.ToStructureIndex, command.NumberOfUnits);

            if (toConnectsResult.IsSuccess)
                spanEquipmentsToConnect.Add(toSpanEquipment.Id, toConnectsResult.Value);
            else
                return Result.Fail(toConnectsResult.Errors.First());

            return Result.Ok(spanEquipmentsToConnect);
        }

        private static Result<SpanEquipmentWithConnectsHolder> GetConnects(SpanEquipment spanEquipment, Guid routeNodeId, ushort startStructureIndex, int numberOfUnits)
        {
            // Create from span equipment connects
            var spanEquipmentConnects = new SpanEquipmentWithConnectsHolder(spanEquipment)
            {
                Connects = new List<SpanSegmentConnectHolder>()
            };

            for (ushort structureIndex = startStructureIndex; structureIndex < startStructureIndex + numberOfUnits; structureIndex++)
            {
                if (TryFindIngoingSegment(spanEquipment, spanEquipment.SpanStructures[structureIndex], routeNodeId, out var ingoingSegment))
                {
                    spanEquipmentConnects.Connects.Add(
                       new SpanSegmentConnectHolder(
                              new SpanSegmentToSimpleTerminalConnectInfo(
                                  segmentId: ingoingSegment.Id,
                                  terminalId: Guid.Empty
                              )
                          )
                       {
                           StructureIndex = structureIndex
                       }
                    );
                }
                else if (TryFindOutgoingSegment(spanEquipment, spanEquipment.SpanStructures[structureIndex], routeNodeId, out var outgoingSegment))
                {
                    spanEquipmentConnects.Connects.Add(
                       new SpanSegmentConnectHolder(
                              new SpanSegmentToSimpleTerminalConnectInfo(
                                  segmentId: outgoingSegment.Id,
                                  terminalId: Guid.Empty
                              )
                          )
                       {
                           StructureIndex = structureIndex
                       }
                    );
                }
                else
                {
                    return Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(ConnectSpanSegmentsAtRouteNodeErrorCodes.SPAN_EQUIPMENT_AND_ROUTE_NODE_WHERE_TO_CONNECT_IS_NOT_COLOCATED, $"No segment from in span equipment with id: {spanEquipment.Id} that is starting or ending in route node with id: {routeNodeId}"));
                }
            }

            return Result.Ok(spanEquipmentConnects);
        }

        private static bool TryFindIngoingSegment(SpanEquipment spanEquipment, SpanStructure spanStructure, Guid routeNodeId, out SpanSegment ingoingSegment)
        {
            foreach (var segment in spanStructure.SpanSegments)
            {
                if (spanEquipment.NodesOfInterestIds[segment.ToNodeOfInterestIndex] == routeNodeId)
                {
                    ingoingSegment = segment;
                    return true;
                }
            }

            ingoingSegment = null;
            return false;
        }

        private static bool TryFindOutgoingSegment(SpanEquipment spanEquipment, SpanStructure spanStructure, Guid routeNodeId, out SpanSegment outgoingSegment)
        {
            foreach (var segment in spanStructure.SpanSegments)
            {
                if (spanEquipment.NodesOfInterestIds[segment.FromNodeOfInterestIndex] == routeNodeId)
                {
                    outgoingSegment = segment;
                    return true;
                }
            }

            outgoingSegment = null;
            return false;
        }

        private async void NotifyExternalServicesAboutConnectivityChange(Guid firstSpanEquipmentId, Guid secondSpanEquipmentId, Guid routeNodeId, string category)
        {
            List<IdChangeSet> idChangeSets = new List<IdChangeSet>
            {
                new IdChangeSet("SpanEquipment", ChangeTypeEnum.Modification, new Guid[] { firstSpanEquipmentId, secondSpanEquipmentId })
            };

            var updatedEvent =
                new RouteNetworkElementContainedEquipmentUpdated(
                    eventType: typeof(RouteNetworkElementContainedEquipmentUpdated).Name,
                    eventId: Guid.NewGuid(),
                    eventTimestamp: DateTime.UtcNow,
                    applicationName: "UtilityNetworkService",
                    applicationInfo: null,
                    category: category,
                    idChangeSets: idChangeSets.ToArray(),
                    affectedRouteNetworkElementIds: new Guid[] { routeNodeId }
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }


        private class SpanEquipmentWithConnectsHolder
        {
            public SpanEquipment SpanEquipment { get; }
            public List<SpanSegmentConnectHolder> Connects { get; set; }

            public SpanEquipmentWithConnectsHolder(SpanEquipment spanEquipment)
            {
                SpanEquipment = spanEquipment;
                Connects = new();
            }
        }

        private class SpanSegmentConnectHolder
        {
            public SpanSegmentToSimpleTerminalConnectInfo ConnectInfo { get; }
            public Guid StructureSpecificationId { get; set; }
            public ushort StructureIndex { get; set; }
            public SpanSegmentConnectHolder(SpanSegmentToSimpleTerminalConnectInfo connectInfo)
            {
                ConnectInfo = connectInfo;
            }
        }
    }
}
