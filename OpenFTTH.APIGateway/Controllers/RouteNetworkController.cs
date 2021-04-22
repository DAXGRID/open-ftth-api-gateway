using System;
using System.Threading.Tasks;
using FluentResults;
using Microsoft.AspNetCore.Mvc;
using OpenFTTH.CQRS;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;

namespace OpenFTTH.APIGateway.RouteNetwork
{
    [Route("api/[controller]")]
    [ApiController]
    public class RouteNetworkController : ControllerBase
    {
        private readonly IQueryDispatcher _queryDispatcher;

        public RouteNetworkController(IQueryDispatcher queryDispatcher)
        {
            _queryDispatcher = queryDispatcher;
        }

        [HttpGet("HasRelatedEquipment/{id}")]
        public async Task<IActionResult> HasRelatedEquipment(string id)
        {
            if (!Guid.TryParse(id, out Guid routeNodeId))
            {
                return BadRequest($"{id} is not a valid guid");
            }

            var routeNodeQuery = new GetRouteNetworkDetails(new RouteNetworkElementIdList() { routeNodeId })
            {
                RelatedInterestFilter = RelatedInterestFilterOptions.ReferencesFromRouteElementOnly
            };

            var queryResult = await _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(routeNodeQuery);

            if (queryResult.IsFailed)
            {
                return BadRequest("Request failed");
            }

            var routeNetworkElement = queryResult.Value.RouteNetworkElements[routeNodeId];

            var hasRelatedEquipment = (routeNetworkElement.InterestRelations != null && routeNetworkElement.InterestRelations.Length > 0);

            return Ok(hasRelatedEquipment);
        }
    }
}
