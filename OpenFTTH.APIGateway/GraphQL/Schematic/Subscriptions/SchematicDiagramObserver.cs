﻿using DAX.EventProcessing.Dispatcher;
using FluentResults;
using Microsoft.Extensions.Logging;
using OpenFTTH.CQRS;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.Schematic.API.Model.DiagramLayout;
using OpenFTTH.Schematic.API.Queries;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace OpenFTTH.APIGateway.GraphQL.Schematic.Subscriptions
{
    public class SchematicDiagramObserver : IObserver<RouteNetworkElementContainedEquipmentUpdated>
    {
        private readonly ILogger<SchematicDiagramObserver> _logger;
        private readonly IToposTypedEventObservable<RouteNetworkElementContainedEquipmentUpdated> _toposTypedEventObserable;
        private readonly IQueryDispatcher _queryDispatcher;

        private ConcurrentDictionary<Guid, Subject<Diagram>> _observableByRouteNetworkElementId = new ConcurrentDictionary<Guid, Subject<Diagram>>();

        public SchematicDiagramObserver(ILogger<SchematicDiagramObserver> logger, IToposTypedEventObservable<RouteNetworkElementContainedEquipmentUpdated> toposTypedEventObserable, IQueryDispatcher queryDispatcher)
        {
            _logger = logger;
            _toposTypedEventObserable = toposTypedEventObserable;
            _queryDispatcher = queryDispatcher;
            _toposTypedEventObserable.OnEvent.Subscribe(this);
        }

        public IObservable<Diagram> WhenDiagramNeedsUpdate(Guid routeNetworkElementId)
        {
            return GetObservable(routeNetworkElementId).AsObservable();
        }

        private Subject<Diagram> GetObservable(Guid routeNetworkElementId)
        {
            return _observableByRouteNetworkElementId.GetOrAdd(routeNetworkElementId, new Subject<Diagram>());
        }

        public void Ping(Guid routeNetworkElementId)
        {
            if (_observableByRouteNetworkElementId.TryGetValue(routeNetworkElementId, out var observable))
            {
                observable.OnNext(GetDiagram(routeNetworkElementId));
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        void IObserver<RouteNetworkElementContainedEquipmentUpdated>.OnNext(RouteNetworkElementContainedEquipmentUpdated @event)
        {
            foreach (var routeNetworkElementId in @event.AffectedRouteNetworkElementIds)
            {
                if (_observableByRouteNetworkElementId.TryGetValue(routeNetworkElementId, out var observable))
                {
                    observable.OnNext(GetDiagram(routeNetworkElementId));
                }
            }
        }
        
        private Diagram GetDiagram(Guid routeNetworkElementId)
        {
            // We catch all execeptions to avoid Topos retrying (calling the message handler again and again)
            // It does not matter that the failed event is never processed again, because it's just a notification topic
            try
            {
                var getDiagramQueryResult = _queryDispatcher.HandleAsync<GetDiagram, Result<GetDiagramResult>>(new GetDiagram(routeNetworkElementId)).Result;

                if (getDiagramQueryResult.IsFailed)
                {
                    _logger.LogError($"Schematic diagram creation for route node element with id: {routeNetworkElementId} failed with message: {getDiagramQueryResult.Errors.First().Message}");
                    return new Diagram();
                }

                return getDiagramQueryResult.Value.Diagram;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Schematic diagram creation for route node element with id: {routeNetworkElementId} failed with message: {ex.Message}", ex);
                return new Diagram();
            }
        }
    }
}
