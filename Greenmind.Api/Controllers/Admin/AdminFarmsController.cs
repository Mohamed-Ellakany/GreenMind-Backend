using Greenmind.Application.Contracts.Admin;
using Greenmind.Application.Dtos.Admin;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Greenmind.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/farms")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminFarmsController(IPlatformAdminService adminService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<FarmAccountDto>>> GetFarms(
        [FromQuery] FarmAccountStatus? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await adminService.GetFarmsAsync(
            status,
            pageNumber,
            pageSize,
            cancellationToken));
    }

    [HttpPost("{farmId:int}/approve")]
    public async Task<ActionResult<FarmAccountDto>> ApproveFarm(
        int farmId,
        CancellationToken cancellationToken)
    {
        var result = await adminService.ApproveFarmAsync(farmId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPatch("{farmId:int}/status")]
    public async Task<ActionResult<FarmAccountDto>> SetAccountStatus(
        int farmId,
        [FromBody] FarmAccountStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await adminService.SetFarmAccountStatusAsync(
            farmId,
            request.IsActive!.Value,
            cancellationToken);

        return ToActionResult(result);
    }

    private ActionResult<FarmAccountDto> ToActionResult(AdminOperationResult<FarmAccountDto> result) =>
        result.Status switch
        {
            AdminOperationStatus.Succeeded => Ok(result.Value),
            AdminOperationStatus.NotFound => NotFound(result.ErrorMessage),
            AdminOperationStatus.Conflict => Conflict(result.ErrorMessage),
            _ => throw new InvalidOperationException("Unsupported admin operation status.")
        };
}

public sealed record FarmAccountStatusRequest([property: Required] bool? IsActive);
