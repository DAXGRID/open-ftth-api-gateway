using DAX.ObjectVersioning.Graph;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.Settings;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Projections;
using System;
using System.IO;
using System.Linq;

namespace OpenFTTH.APIGateway.Conversion
{
    public class Checker : ImporterBase
    {
        private ILogger<Checker> _logger;
        private Guid _workTaskId;
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;
        private UtilityNetworkProjection _utilityNetwork;


        public Checker(ILogger<Checker> logger, Guid workTaskId, IEventStore eventSTore, GeoDatabaseSetting geoDatabaseSettings, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher) : base(geoDatabaseSettings)
        {
            _logger = logger;
            _workTaskId = workTaskId;
            _eventStore = eventSTore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;

            _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
        }


        public void Run()
        {
            _logger.LogInformation("Checker started...");

            var terminalEquipmentSpecifications = _eventStore.Projections.Get<TerminalEquipmentSpecificationsProjection>().Specifications;

            var terminalEquipments = _utilityNetwork.TerminalEquipmentByEquipmentId.Values;

            using StreamWriter csvFile = new("c:/temp/openftth_kunde_check.csv");

            var csvHeader = "\"instnr\";\"rammer_odf\"";
            csvFile.WriteLine(csvHeader);

            foreach (var terminalEquipment in terminalEquipments)
            {
                var spec = terminalEquipmentSpecifications[terminalEquipment.SpecificationId];

                if (spec.Name == "Kundeterminering")
                {
                    // trace terminal one

                    var traceResult = _utilityNetwork.Graph.Trace(terminalEquipment.TerminalStructures[0].Terminals[0].Id);

                    string endsInRack = "nej";

                    if (CheckIfEndTerminalIsWithinRackEquipment(terminalEquipmentSpecifications, traceResult.Upstream))
                        endsInRack = "ja";
                    else if (CheckIfEndTerminalIsWithinRackEquipment(terminalEquipmentSpecifications, traceResult.Downstream))
                        endsInRack = "ja";



                    var csvLine = "\"" + terminalEquipment.Name + "\";\"" + endsInRack + "\"";

                    csvFile.WriteLine(csvLine);
                }
            }

            csvFile.Close();

            _logger.LogInformation("Checker finish!");
        }

        private bool CheckIfEndTerminalIsWithinRackEquipment(LookupCollection<TerminalEquipmentSpecification> terminalEquipmentSpecifications, IGraphObject[] graphObjects)
        {
            if (graphObjects.Length > 0)
            {
                var upstreamTerminal = graphObjects.Last();

                if (upstreamTerminal != null)
                {
                    if (_utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphTerminalRef>(upstreamTerminal.Id, out var terminalRef))
                    {
                        if (!terminalRef.IsDummyEnd)
                        {
                            var terminalEquipment = terminalRef.TerminalEquipment(_utilityNetwork);

                            var spec = terminalEquipmentSpecifications[terminalEquipment.SpecificationId];

                            if (spec.IsRackEquipment)
                                return true;
                        }
                    }
                }
            }

            return false;
        }


    }
}

