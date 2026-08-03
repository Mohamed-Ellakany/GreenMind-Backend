using System.Net.Mail;
using Greenmind.Api.Services;
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
public sealed class AdminFarmsController(
    IPlatformAdminService adminService,
    IAccountEmailService emailService,
    ILogger<AdminFarmsController> logger) : ControllerBase
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
        if (result is { Status: AdminOperationStatus.Succeeded, Value: not null })
        {
            await TrySendApprovalEmailAsync(result.Value, cancellationToken);
        }
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

    private async Task TrySendApprovalEmailAsync(
        FarmAccountDto farm,
        CancellationToken cancellationToken)
    {
        if (!emailService.IsConfigured || string.IsNullOrWhiteSpace(farm.OwnerEmail))
        {
            return;
        }

        try
        {
            await emailService.SendFarmApprovedAsync(
                farm.OwnerEmail,
                farm.OwnerName,
                farm.Name,
                cancellationToken);
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Approval email could not be sent for farm {FarmId}.", farm.Id);
        }
    }
}

public sealed class FarmAccountStatusRequest
{
    [Required]
    public bool? IsActive { get; init; }
}
