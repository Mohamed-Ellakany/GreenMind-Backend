using Greenmind.Application.Dtos.AI;
using Greenmind.Infrastructure.Agents;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FarmPlanController : ControllerBase
    {
        private readonly AgentPlanningService _service;
        public FarmPlanController(AgentPlanningService service) => _service = service;

        [HttpPost("generate-tasks")]
        public async Task<ActionResult<AgentResponse>> GenerateTasks([FromBody] AgentRequest request)
        {
            var result = await _service.GeneratePlanAsync(request);
            return Ok(result);
        }
    }
}