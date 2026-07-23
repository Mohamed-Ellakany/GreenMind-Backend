using Greenmind.Application.Contracts.Admin;
using Greenmind.Application.Dtos.Admin;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/statistics")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminStatisticsController(IPlatformAdminService adminService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PlatformStatisticsDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await adminService.GetStatisticsAsync(cancellationToken));
    }
}
