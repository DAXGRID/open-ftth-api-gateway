using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.Reporting;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.Business.RouteElements.StateHandling;

namespace OpenFTTH.APIGateway.RouteNetwork.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ReportController : ControllerBase
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly IEventStore _eventStore;
        private readonly IRouteNetworkState _routeNetworkState;

        public ReportController(
            ILoggerFactory loggerFactory,
            IEventStore eventStore,
            IRouteNetworkState routeNetworkState)
        {
            _loggerFactory = loggerFactory;
            _eventStore = eventStore;
            _routeNetworkState = routeNetworkState;
        }

        [HttpGet("CustomerTerminationReport")]
        public async Task CustomerTerminationReport()
        {
            var traceReport = new CustomerTerminationReport(
                _loggerFactory.CreateLogger<CustomerTerminationReport>(),
                _eventStore,
                _routeNetworkState);

            Response.ContentType = "application/text";
            Response.StatusCode = 200;

            await using (var writer = new StreamWriter(Response.Body, Encoding.UTF8))
            {
                foreach (var line in traceReport.TraceAllCustomerTerminations())
                {
                    await writer.WriteLineAsync(line);
                    // Ensure the line is sent to the client immediately
                    await writer.FlushAsync();
                }
            }
        }
    }
}
