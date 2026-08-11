using Greenmind.Application.Dtos.AI;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Domain.Consts;
using Greenmind.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Greenmind.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = RoleNames.FarmOwner)]
    public class FarmPlanController : ControllerBase
    {
        private readonly IAgentPlanningService _service;
        private readonly GreenmindDbContext _dbContext;

        public FarmPlanController(
            IAgentPlanningService service,
            GreenmindDbContext dbContext)
        {
            _service = service;
            _dbContext = dbContext;
        }

        [HttpPost("generate-tasks")]
        public async Task<ActionResult<AgentResponse>> GenerateTasks([FromBody] AgentRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();
            if (request.ExportPlan is null || request.ExportPlan.ExportPlanId <= 0)
                return BadRequest("A valid export plan is required.");

            var ownsPlan = await _dbContext.ExportPlans
                .AsNoTracking()
                .AnyAsync(plan =>
                    plan.Id == request.ExportPlan.ExportPlanId &&
                    plan.Farm.OwnerUserId == userId,
                    HttpContext.RequestAborted);
            if (!ownsPlan)
                return NotFound();

            var result = await _service.GeneratePlanAsync(request);
            return Ok(result);
        }
    }
}
