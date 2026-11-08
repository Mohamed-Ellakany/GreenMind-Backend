using System.Security.Claims;
using Greenmind.Api.Models;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Contracts.Disease;
using Greenmind.Application.Dtos.Disease;
using Greenmind.Application.Dtos.TaskProposals;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Greenmind.Api.Controllers.FarmOwner;

[ApiController]
[Route("api/export-plans/{exportPlanId:int}/diagnoses")]
[Authorize(Roles = RoleNames.FarmOwner)]
public sealed class DiagnosisSessionsController(
    IDiagnosisSessionService diagnosisService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<DiagnosisDetailDto>> Create(
        int exportPlanId,
        CancellationToken cancellationToken)
    {
        var result = await diagnosisService.CreateAsync(exportPlanId, OwnerId(), cancellationToken);
        return result.Status == DiseaseOperationStatus.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : Map(result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DiagnosisSummaryDto>>> List(
        int exportPlanId,
        CancellationToken cancellationToken) =>
        Map(await diagnosisService.ListAsync(exportPlanId, OwnerId(), cancellationToken));

    [HttpGet("{diagnosisId:int}")]
    public async Task<ActionResult<DiagnosisDetailDto>> Get(
        int exportPlanId,
        int diagnosisId,
        CancellationToken cancellationToken) =>
        Map(await diagnosisService.GetAsync(exportPlanId, diagnosisId, OwnerId(), cancellationToken));

    [HttpPost("{diagnosisId:int}/messages")]
    [EnableRateLimiting("ai-chat")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 30 * 1024 * 1024)]
    public async Task<ActionResult<DiagnosisTurnDto>> SendMessage(
        int exportPlanId,
        int diagnosisId,
        [FromForm] DiagnosisMessageUploadRequest request,
        CancellationToken cancellationToken)
    {
        var images = new List<IncomingPrivateFile>(request.Images.Count);
        foreach (var image in request.Images)
        {
            await using var input = image.OpenReadStream();
            using var buffer = new MemoryStream();
            await input.CopyToAsync(buffer, cancellationToken);
            images.Add(new IncomingPrivateFile(image.FileName, image.ContentType, buffer.ToArray()));
        }
        return Map(await diagnosisService.SendMessageAsync(
            exportPlanId,
            diagnosisId,
            OwnerId(),
            new CreateDiagnosisMessageRequest(request.Content ?? string.Empty, request.IdempotencyKey, images),
            cancellationToken));
    }

    [HttpPost("{diagnosisId:int}/finalize")]
    [EnableRateLimiting("ai-analysis")]
    public async Task<ActionResult<DiagnosisDetailDto>> FinalizeDiagnosis(
        int exportPlanId,
        int diagnosisId,
        [FromBody] FinalizeDiagnosisApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await diagnosisService.FinalizeAsync(
            exportPlanId, diagnosisId, OwnerId(), request.Force, cancellationToken);
        return result.Status == DiseaseOperationStatus.Succeeded
            ? StatusCode(StatusCodes.Status202Accepted, result.Value)
            : Map(result);
    }

    [HttpPost("{diagnosisId:int}/cancel")]
    public async Task<ActionResult<DiagnosisDetailDto>> Cancel(
        int exportPlanId,
        int diagnosisId,
        CancellationToken cancellationToken) =>
        Map(await diagnosisService.CancelAsync(exportPlanId, diagnosisId, OwnerId(), cancellationToken));

    [HttpPost("{diagnosisId:int}/retry")]
    [EnableRateLimiting("ai-analysis")]
    public async Task<ActionResult<DiagnosisDetailDto>> Retry(
        int exportPlanId,
        int diagnosisId,
        CancellationToken cancellationToken) =>
        Map(await diagnosisService.RetryAsync(exportPlanId, diagnosisId, OwnerId(), cancellationToken));

    [HttpGet("{diagnosisId:int}/images/{imageId:int}")]
    [EnableRateLimiting("private-files")]
    public async Task<IActionResult> DownloadImage(
        int exportPlanId,
        int diagnosisId,
        int imageId,
        CancellationToken cancellationToken)
    {
        var result = await diagnosisService.DownloadImageAsync(
            exportPlanId, diagnosisId, imageId, OwnerId(), cancellationToken);
        if (result.Status == DiseaseOperationStatus.NotFound) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Value!.Content, result.Value.ContentType, result.Value.FileName, true);
    }

    [HttpPost("{diagnosisId:int}/treatment-proposal")]
    [EnableRateLimiting("ai-analysis")]
    public async Task<ActionResult<TaskChangeProposalDto>> CreateTreatmentProposal(
        int exportPlanId,
        int diagnosisId,
        CancellationToken cancellationToken) =>
        Map(await diagnosisService.CreateTreatmentProposalAsync(
            exportPlanId, diagnosisId, OwnerId(), cancellationToken));

    private ActionResult<T> Map<T>(DiseaseOperationResult<T> result) => result.Status switch
    {
        DiseaseOperationStatus.Succeeded => Ok(result.Value),
        DiseaseOperationStatus.NotFound => NotFound(),
        DiseaseOperationStatus.Conflict => Conflict(ApiResponseFactory.Failure<T>(
            StatusCodes.Status409Conflict,
            result.ErrorCode ?? ApiErrorCodes.Conflict,
            result.ErrorMessage ?? "تعارضت حالة جلسة التشخيص.",
            HttpContext.TraceIdentifier)),
        _ => BadRequest(ApiResponseFactory.Failure<T>(
            StatusCodes.Status400BadRequest,
            result.ErrorCode ?? ApiErrorCodes.DiseaseImageInvalid,
            result.ErrorMessage ?? "بيانات جلسة التشخيص غير صالحة.",
            HttpContext.TraceIdentifier))
    };

    private string OwnerId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("An authenticated user id is required.");
}
