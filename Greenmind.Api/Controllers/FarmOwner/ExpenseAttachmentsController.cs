using System.Security.Claims;
using Greenmind.Api.Models;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Contracts.Costs;
using Greenmind.Application.Dtos.Costs;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers.FarmOwner;

[ApiController]
[Route("api/export-plans/{exportPlanId:int}/expenses/{expenseId:int}/attachments")]
[Authorize(Roles = RoleNames.FarmOwner)]
public sealed class ExpenseAttachmentsController(
    IExpenseAttachmentService attachmentService) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
    public async Task<ActionResult<ExpenseAttachmentDto>> Upload(
        int exportPlanId,
        int expenseId,
        [FromForm] ExpenseAttachmentUploadRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null ||
            !ArabicBusinessValues.TryParseArabic(
                request.AttachmentType,
                out ExpenseAttachmentType attachmentType))
        {
            return BadRequest(ApiResponseFactory.Failure<ExpenseAttachmentDto>(
                StatusCodes.Status400BadRequest,
                ApiErrorCodes.AttachmentInvalid,
                "نوع المرفق أو الملف غير صالح.",
                HttpContext.TraceIdentifier));
        }

        await using var stream = request.File.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var result = await attachmentService.UploadAsync(
            exportPlanId,
            expenseId,
            GetOwnerUserId(),
            new ExpenseAttachmentUpload(
                attachmentType,
                new IncomingCostFile(
                    request.File.FileName,
                    request.File.ContentType,
                    buffer.ToArray())),
            cancellationToken);

        return result.Status == CostOperationStatus.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : Map(result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExpenseAttachmentDto>>> List(
        int exportPlanId,
        int expenseId,
        CancellationToken cancellationToken)
    {
        var result = await attachmentService.ListAsync(
            exportPlanId,
            expenseId,
            GetOwnerUserId(),
            cancellationToken);
        return result.Status == CostOperationStatus.NotFound
            ? NotFound()
            : Ok(result.Value);
    }

    [HttpGet("{attachmentId:int}/download")]
    public async Task<IActionResult> Download(
        int exportPlanId,
        int expenseId,
        int attachmentId,
        CancellationToken cancellationToken)
    {
        var result = await attachmentService.DownloadAsync(
            exportPlanId,
            expenseId,
            attachmentId,
            GetOwnerUserId(),
            cancellationToken);
        if (result.Status == CostOperationStatus.NotFound)
            return NotFound();

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(
            result.Value!.Content,
            result.Value.ContentType,
            result.Value.FileName,
            enableRangeProcessing: true);
    }

    [HttpDelete("{attachmentId:int}")]
    public async Task<IActionResult> Delete(
        int exportPlanId,
        int expenseId,
        int attachmentId,
        CancellationToken cancellationToken)
    {
        var result = await attachmentService.DeleteAsync(
            exportPlanId,
            expenseId,
            attachmentId,
            GetOwnerUserId(),
            cancellationToken);
        return result.Status == CostOperationStatus.NotFound
            ? NotFound()
            : NoContent();
    }

    private ActionResult<ExpenseAttachmentDto> Map(
        CostOperationResult<ExpenseAttachmentDto> result) =>
        result.Status switch
        {
            CostOperationStatus.Succeeded => Ok(result.Value),
            CostOperationStatus.NotFound => NotFound(),
            _ => BadRequest(ApiResponseFactory.Failure<ExpenseAttachmentDto>(
                StatusCodes.Status400BadRequest,
                result.ErrorCode ?? ApiErrorCodes.AttachmentInvalid,
                result.ErrorMessage ?? "المرفق غير صالح.",
                HttpContext.TraceIdentifier))
        };

    private string GetOwnerUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("An authenticated user id is required.");
}
