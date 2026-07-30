using Greenmind.Api.Models;
using Greenmind.Domain.Consts;
using Greenmind.Infrastructure.Rag;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers.Admin
{
    /// <summary>
    /// One-off / occasional-use endpoints to ingest compliance source documents into the
    /// RAG knowledge base. Not meant to be called by farmers — restrict to PlatformAdmin.
    /// </summary>
    [Route("api/admin/rag")]
    [ApiController]
    [Authorize(Roles = RoleNames.Admin)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumRequestSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaximumRequestSize)]
    public class RagAdminController : ControllerBase
    {
        private const long MaximumRequestSize = 25 * 1024 * 1024;
        private readonly IComplianceIngestionService _ingestionService;

        public RagAdminController(IComplianceIngestionService ingestionService)
        {
            _ingestionService = ingestionService;
        }

        /// <summary>
        /// Ingests a UNECE FFV-style quality/grading standard PDF (e.g. FFV-35 Strawberries).
        /// </summary>
        [HttpPost("ingest/quality-standard")]
        public async Task<IActionResult> IngestQualityStandard(
            [FromForm] QualityStandardIngestionRequest request,
            CancellationToken cancellationToken)
        {
            var validationError = await ValidatePdfAsync(request.File, cancellationToken);
            if (validationError is not null)
                return BadRequest(validationError);

            await using var stream = request.File!.OpenReadStream();
            var count = await _ingestionService.IngestFfvStandardAsync(
                stream, request.Crop.Trim(), request.SourceName.Trim(), cancellationToken);

            return Ok(new { ChunksIngested = count });
        }

        /// <summary>
        /// Ingests the body (Articles) of an EU regulation PDF (e.g. Regulation (EU) 2019/2072).
        /// </summary>
        [HttpPost("ingest/eu-regulation-body")]
        public async Task<IActionResult> IngestEuRegulationBody(
            [FromForm] EuRegulationBodyIngestionRequest request,
            CancellationToken cancellationToken)
        {
            var validationError = await ValidatePdfAsync(request.File, cancellationToken);
            if (validationError is not null)
                return BadRequest(validationError);

            await using var stream = request.File!.OpenReadStream();
            var count = await _ingestionService.IngestEuRegulationBodyAsync(
                stream, request.SourceName.Trim(), cancellationToken);

            return Ok(new { ChunksIngested = count });
        }

        /// <summary>
        /// Ingests only the annex paragraphs mentioning your crop's keywords (e.g. "Fragaria",
        /// "strawberr") instead of the entire annex - keeps the knowledge base focused for a
        /// single-crop project and avoids paying embedding cost for irrelevant host genera.
        /// </summary>
        [HttpPost("ingest/eu-regulation-annex")]
        public async Task<IActionResult> IngestEuRegulationAnnex(
            [FromForm] EuRegulationAnnexIngestionRequest request,
            CancellationToken cancellationToken)
        {
            var validationError = await ValidatePdfAsync(request.File, cancellationToken);
            if (validationError is not null)
                return BadRequest(validationError);

            var keywords = request.CropKeywordsCsv.Split(
                ',',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (keywords.Length == 0 || keywords.Any(keyword => keyword.Length > 100))
                return BadRequest("At least one valid crop keyword is required.");

            await using var stream = request.File!.OpenReadStream();
            var count = await _ingestionService.IngestEuRegulationAnnexForCropAsync(
                stream,
                request.SourceName.Trim(),
                request.AnnexLabelHint.Trim(),
                keywords,
                cancellationToken);

            return Ok(new { ChunksIngested = count });
        }

        private static async Task<string?> ValidatePdfAsync(
            IFormFile? file,
            CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
                return "A non-empty PDF file is required.";
            if (file.Length > MaximumRequestSize)
                return "The PDF exceeds the 25 MB limit.";

            byte[] signature = new byte[5];
            await using var stream = file.OpenReadStream();
            var bytesRead = await stream.ReadAsync(signature, cancellationToken);
            return bytesRead == signature.Length &&
                   signature.AsSpan().SequenceEqual("%PDF-"u8)
                ? null
                : "The uploaded file is not a valid PDF.";
        }
    }
}
