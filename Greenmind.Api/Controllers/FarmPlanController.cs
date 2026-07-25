using Greenmind.Application.Dtos.AI;
using Greenmind.Application.Interfaces.AI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FarmPlanController : ControllerBase
    {
        private readonly IAgentPlanningService _service;
        public FarmPlanController(IAgentPlanningService service) => _service = service;

        [HttpPost("generate-tasks")]
        public async Task<ActionResult<AgentResponse>> GenerateTasks([FromBody] AgentRequest request)
        {
            var result = await _service.GeneratePlanAsync(request);
            return Ok(result);
        }
    }
}
