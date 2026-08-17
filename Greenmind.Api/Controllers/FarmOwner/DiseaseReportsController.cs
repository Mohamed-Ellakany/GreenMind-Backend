using System.Security.Claims;
using Greenmind.Api.Models;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Contracts.Disease;
using Greenmind.Application.Dtos.Disease;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Greenmind.Api.Controllers.FarmOwner;

[ApiController]
[Route("api/export-plans/{exportPlanId:int}/disease-reports")]
[Authorize(Roles = RoleNames.FarmOwner)]
public sealed class DiseaseReportsController(
    IDiseaseReportService diseaseService) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("ai-analysis")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 30 * 1024 * 1024)]
    public async Task<ActionResult<DiseaseReportDto>> Submit(
        int exportPlanId,
        [FromForm] DiseaseReportUploadRequest request,
        CancellationToken cancellationToken)
    {
        var files = new List<IncomingPrivateFile>(request.Images.Count);
        foreach (var file in request.Images)
        {
            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            files.Add(new IncomingPrivateFile(
                file.FileName,
                file.ContentType,
                buffer.ToArray()));
        }

        var result = await diseaseService.SubmitAsync(
            exportPlanId,
            GetOwnerUserId(),
            new CreateDiseaseReportRequest(
                request.ObservationDate,
                request.AffectedArea,
                request.Notes,
                files),
            cancellationToken);
        return result.Status == DiseaseOperationStatus.Succeeded
            ? StatusCode(StatusCodes.Status202Accepted, result.Value)
            : Map(result);
    }

    [HttpGet]
    public async Task<ActionResult<DiseaseReportPageDto>> List(
        int exportPlanId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await diseaseService.ListAsync(
            exportPlanId,
            GetOwnerUserId(),
            page,
            pageSize,
            cancellationToken);
        return result.Status == DiseaseOperationStatus.NotFound
            ? NotFound()
            : Ok(result.Value);
    }

    [HttpGet("{reportId:int}")]
    public async Task<ActionResult<DiseaseReportDto>> Get(
        int exportPlanId,
        int reportId,
        CancellationToken cancellationToken)
    {
        var result = await diseaseService.GetAsync(
            exportPlanId,
            reportId,
            GetOwnerUserId(),
            cancellationToken);
        return Map(result);
    }

    [HttpGet("{reportId:int}/images/{imageId:int}")]
    [EnableRateLimiting("private-files")]
    public async Task<IActionResult> DownloadImage(
        int exportPlanId,
        int reportId,
        int imageId,
        CancellationToken cancellationToken)
    {
        var result = await diseaseService.DownloadImageAsync(
            exportPlanId,
            reportId,
            imageId,
            GetOwnerUserId(),
            cancellationToken);
        if (result.Status == DiseaseOperationStatus.NotFound)
            return NotFound();

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(
            result.Value!.Content,
            result.Value.ContentType,
            result.Value.FileName,
            enableRangeProcessing: true);
    }

    [HttpPost("{reportId:int}/retry")]
    [EnableRateLimiting("ai-analysis")]
    public async Task<ActionResult<DiseaseReportDto>> Retry(
        int exportPlanId,
        int reportId,
        CancellationToken cancellationToken)
    {
        var result = await diseaseService.RetryAsync(
            exportPlanId,
            reportId,
            GetOwnerUserId(),
            cancellationToken);
        return Map(result);
    }

    private ActionResult<DiseaseReportDto> Map(
        DiseaseOperationResult<DiseaseReportDto> result) =>
        result.Status switch
        {
            DiseaseOperationStatus.Succeeded => Ok(result.Value),
            DiseaseOperationStatus.NotFound => NotFound(),
            DiseaseOperationStatus.Conflict => Conflict(
                ApiResponseFactory.Failure<DiseaseReportDto>(
                    StatusCodes.Status409Conflict,
                    result.ErrorCode ?? ApiErrorCodes.Conflict,
                    result.ErrorMessage ?? "تعذر إعادة تحليل التقرير.",
                    HttpContext.TraceIdentifier)),
            _ => BadRequest(ApiResponseFactory.Failure<DiseaseReportDto>(
                StatusCodes.Status400BadRequest,
                result.ErrorCode ?? ApiErrorCodes.DiseaseImageInvalid,
                result.ErrorMessage ?? "صور المرض غير صالحة.",
                HttpContext.TraceIdentifier))
        };

    private string GetOwnerUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("An authenticated user id is required.");
}
