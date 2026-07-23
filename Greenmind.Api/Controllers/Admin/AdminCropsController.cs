using Greenmind.Application.Contracts.Admin;
using Greenmind.Application.Dtos.Admin;
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
        CancellationToken cancellationToken)
    {
        return Ok(await adminService.GetCropsAsync(cancellationToken));
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

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await adminService.DeleteCropAsync(id, cancellationToken);
        return result.Status switch
        {
            AdminOperationStatus.Succeeded => Ok(new { Deleted = true }),
            AdminOperationStatus.NotFound => NotFound(result.ErrorMessage),
            AdminOperationStatus.Conflict => Conflict(result.ErrorMessage),
            _ => throw new InvalidOperationException("Unsupported admin operation status.")
        };
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
