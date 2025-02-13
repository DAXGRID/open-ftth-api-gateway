using FluentAssertions;
using FluentResults;
using Newtonsoft.Json;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.Tests.TestData;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions.Ordering;

namespace OpenFTTH.UtilityGraphService.Tests.TerminalEquipmentConnectivityView
{
    public class SerializationTests
    {
        private IEventStore _eventStore;
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;

        public SerializationTests(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
        }

        [Fact]
        public void SerializeTestdata()
        {
            /*
            var json = JsonConvert.SerializeObject(TestTerminalEquipmentConnectivityViewData.LISAODFRack());


            json = JsonConvert.SerializeObject(TestEquipmentConnectionFaceData.EquipmentConnectivityFaces());

            json = JsonConvert.SerializeObject(TestEquipmentConnectionFaceData.TerminalEquipment_EquipmentConnectivityFaceConnectionInfo());

            json = JsonConvert.SerializeObject(TestEquipmentConnectionFaceData.SpanEquipment_EquipmentConnectivityFaceConnectionInfo());
            */
        }


    }
}
