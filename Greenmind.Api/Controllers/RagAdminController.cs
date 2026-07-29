using Greenmind.Infrastructure.Rag;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers
{
    /// <summary>
    /// One-off / occasional-use endpoints to ingest compliance source documents into the
    /// RAG knowledge base. Not meant to be called by farmers — restrict to PlatformAdmin.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "PlatformAdmin")]
    public class RagAdminController : ControllerBase
    {
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
            [FromForm] IFormFile file,
            [FromForm] string crop,
            [FromForm] string sourceName)
        {
            if (file.Length == 0)
                return BadRequest("Empty file.");

            await using var stream = file.OpenReadStream();
            var count = await _ingestionService.IngestFfvStandardAsync(stream, crop, sourceName);

            return Ok(new { ChunksIngested = count });
        }

        /// <summary>
        /// Ingests the body (Articles) of an EU regulation PDF (e.g. Regulation (EU) 2019/2072).
        /// </summary>
        [HttpPost("ingest/eu-regulation-body")]
        public async Task<IActionResult> IngestEuRegulationBody(
            [FromForm] IFormFile file,
            [FromForm] string sourceName)
        {
            if (file.Length == 0)
                return BadRequest("Empty file.");

            await using var stream = file.OpenReadStream();
            var count = await _ingestionService.IngestEuRegulationBodyAsync(stream, sourceName);

            return Ok(new { ChunksIngested = count });
        }

        /// <summary>
        /// Ingests only the annex paragraphs mentioning your crop's keywords (e.g. "Fragaria",
        /// "strawberr") instead of the entire annex - keeps the knowledge base focused for a
        /// single-crop project and avoids paying embedding cost for irrelevant host genera.
        /// </summary>
        [HttpPost("ingest/eu-regulation-annex")]
        public async Task<IActionResult> IngestEuRegulationAnnex(
            [FromForm] IFormFile file,
            [FromForm] string sourceName,
            [FromForm] string annexLabelHint,
            [FromForm] string cropKeywordsCsv)
        {
            if (file.Length == 0)
                return BadRequest("Empty file.");

            var keywords = cropKeywordsCsv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            await using var stream = file.OpenReadStream();
            var count = await _ingestionService.IngestEuRegulationAnnexForCropAsync(
                stream, sourceName, annexLabelHint, keywords);

            return Ok(new { ChunksIngested = count });
        }
    }
}