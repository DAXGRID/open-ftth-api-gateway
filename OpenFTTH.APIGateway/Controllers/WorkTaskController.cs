using FluentResults;
using Microsoft.AspNetCore.Mvc;
using OpenFTTH.CQRS;
using OpenFTTH.Work.API.Model;
using OpenFTTH.Work.API.Queries;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.APIGateway.RouteNetwork.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WorkTaskController : ControllerBase
    {
        private readonly IQueryDispatcher _queryDispatcher;

        public WorkTaskController(IQueryDispatcher queryDispatcher)
        {
            _queryDispatcher = queryDispatcher;
        }

        [HttpGet("UserWorkTask/{username}")]
        public async Task<IActionResult> UserWorkTask(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return BadRequest("Username cannot be null, empty or whitespace");
            }

            var queryRequest = new GetUserWorkContext(userName);
            var queryResult = await _queryDispatcher.HandleAsync<GetUserWorkContext, Result<UserWorkContext>>(queryRequest);
            if (queryResult.IsSuccess)
            {
                return Ok(queryResult.Value);
            }
            else
            {
                return BadRequest(queryResult.Errors.FirstOrDefault());
            }
        }
    }
}
