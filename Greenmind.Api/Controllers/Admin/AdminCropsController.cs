using Greenmind.Application.Contracts.Admin;
using Greenmind.Application.Dtos.MasterData;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/crops")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminCropsController(IPlatformAdminService adminService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MasterDataItemDto>>> GetAll(
        [FromQuery] bool includeDeleted,
        CancellationToken cancellationToken)
    {
        return Ok(await adminService.GetCropsAsync(includeDeleted, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<MasterDataItemDto>> Create(
        [FromBody] MasterDataRequest request,
        CancellationToken cancellationToken)
    {
        var result = await adminService.CreateCropAsync(request, cancellationToken);
        return result.Status == AdminOperationStatus.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : Conflict(result.ErrorMessage);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<MasterDataItemDto>> Update(
        int id,
        [FromBody] MasterDataRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await adminService.UpdateCropAsync(id, request, cancellationToken));
    }

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<MasterDataItemDto>> SetStatus(
        int id,
        [FromBody] MasterDataStatusRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await adminService.SetCropStatusAsync(
            id,
            request.IsActive!.Value,
            cancellationToken));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<MasterDataItemDto>> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await adminService.DeleteCropAsync(id, cancellationToken));
    }

    [HttpPost("{id:int}/restore")]
    public async Task<ActionResult<MasterDataItemDto>> Restore(
        int id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await adminService.RestoreCropAsync(id, cancellationToken));
    }

    private ActionResult<MasterDataItemDto> ToActionResult(AdminOperationResult<MasterDataItemDto> result) =>
        result.Status switch
        {
            AdminOperationStatus.Succeeded => Ok(result.Value),
            AdminOperationStatus.NotFound => NotFound(result.ErrorMessage),
            AdminOperationStatus.Conflict => Conflict(result.ErrorMessage),
            _ => throw new InvalidOperationException("Unsupported admin operation status.")
        };
}
