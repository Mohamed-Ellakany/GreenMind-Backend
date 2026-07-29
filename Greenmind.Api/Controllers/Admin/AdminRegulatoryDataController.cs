using Greenmind.Api.Models;
using Greenmind.Application.Contracts.Regulatory;
using Greenmind.Application.Dtos.Regulatory;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/regulatory-data")]
[Authorize(Roles = RoleNames.Admin)]
[Consumes("multipart/form-data")]
[RequestSizeLimit(MaximumRequestSize)]
[RequestFormLimits(MultipartBodyLengthLimit = MaximumRequestSize)]
public sealed class AdminRegulatoryDataController(IRegulatoryDataImportService importService)
    : ControllerBase
{
    private const long MaximumRequestSize = 11 * 1024 * 1024;

    [HttpPost("active-substances")]
    [ProducesResponseType<RegulatoryImportResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RegulatoryImportResultDto>> UploadActiveSubstances(
        [FromForm] ActiveSubstancesUploadRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null)
        {
            return BadRequest("ملف Excel مطلوب.");
        }

        try
        {
            await using var stream = request.File.OpenReadStream();
            return Ok(await importService.ImportActiveSubstancesAsync(
                request.RegionName,
                request.File.FileName,
                request.File.Length,
                stream,
                cancellationToken));
        }
        catch (RegulatoryImportValidationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("mrls")]
    [ProducesResponseType<RegulatoryImportResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RegulatoryImportResultDto>> UploadProductMrls(
        [FromForm] ProductMrlsUploadRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null)
        {
            return BadRequest("ملف Excel مطلوب.");
        }

        try
        {
            await using var stream = request.File.OpenReadStream();
            return Ok(await importService.ImportProductMrlsAsync(
                request.RegionName,
                request.ProductType,
                request.File.FileName,
                request.File.Length,
                stream,
                cancellationToken));
        }
        catch (RegulatoryImportValidationException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}
