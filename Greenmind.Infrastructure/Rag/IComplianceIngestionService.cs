using Greenmind.Application.Contracts.Persistence;
using Greenmind.Application.Dtos.AI;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Iti;
using Greenmind.Infrastructure.Rag.Chunkers;
using Microsoft.Extensions.Logging;

namespace Greenmind.Infrastructure.Rag
{
    public interface IComplianceIngestionService
    {
        Task<int> IngestFfvStandardAsync(Stream pdfStream, string crop, string sourceName, CancellationToken ct = default);

        Task<int> IngestEuRegulationBodyAsync(Stream pdfStream, string sourceName, CancellationToken ct = default);

        Task<int> IngestEuRegulationAnnexForCropAsync(
            Stream pdfStream, string sourceName, string annexLabelHint, string[] cropKeywords, CancellationToken ct = default);
    }

    public class ComplianceIngestionService : IComplianceIngestionService
    {
        private readonly IComplianceDocumentRepository _repo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IItiBedrockGatewayClient _gatewayClient;
        private readonly ILogger<ComplianceIngestionService> _logger;

        public ComplianceIngestionService(
            IComplianceDocumentRepository repo,
            IUnitOfWork unitOfWork,
            IItiBedrockGatewayClient gatewayClient,
            ILogger<ComplianceIngestionService> logger)
        {
            _repo = repo;
            _unitOfWork = unitOfWork;
            _gatewayClient = gatewayClient;
            _logger = logger;
        }

        public async Task<int> IngestFfvStandardAsync(
            Stream pdfStream, string crop, string sourceName, CancellationToken ct = default)
        {
            var rawText = PdfTextExtractor.ExtractText(pdfStream);
            var chunks = FfvStandardChunker.Chunk(rawText, sourceName, crop);
            return await EmbedAndSaveAsync(chunks, ct);
        }

        public async Task<int> IngestEuRegulationBodyAsync(
            Stream pdfStream, string sourceName, CancellationToken ct = default)
        {
            var rawText = PdfTextExtractor.ExtractText(pdfStream);
            var chunks = EuRegulationChunker.ChunkByArticles(rawText, sourceName);
            return await EmbedAndSaveAsync(chunks, ct);
        }

        public async Task<int> IngestEuRegulationAnnexForCropAsync(
            Stream pdfStream, string sourceName, string annexLabelHint, string[] cropKeywords, CancellationToken ct = default)
        {
            var rawText = PdfTextExtractor.ExtractText(pdfStream);
            var chunks = EuRegulationChunker.ChunkAnnexFilteredByKeyword(rawText, sourceName, annexLabelHint, cropKeywords);
            return await EmbedAndSaveAsync(chunks, ct);
        }

        private async Task<int> EmbedAndSaveAsync(List<DocumentChunkDto> chunks, CancellationToken ct)
        {
            if (chunks.Count == 0)
            {
                _logger.LogWarning("Chunker produced 0 chunks - nothing to ingest.");
                return 0;
            }

            _logger.LogInformation("Embedding {Count} chunks...", chunks.Count);

            // Batch embedding call - most embedding APIs accept a list of inputs in one request,
            // which is much cheaper/faster than one call per chunk.
            var embeddings = await _gatewayClient.EmbedAsync(
                chunks.Select(c => c.Content).ToList(),
                "search_document",
                ct);

            var entities = new List<ComplianceDocument>(chunks.Count);

            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                entities.Add(new ComplianceDocument
                {
                    Category = chunk.Category,
                    Crop = chunk.Crop,
                    Destination = chunk.Destination,
                    SourceName = chunk.SourceName,
                    SectionReference = chunk.SectionReference,
                    Content = chunk.Content,
                    Embedding = EmbeddingSerializer.Serialize(embeddings[i])
                });
            }

            await _repo.AddRangeAsync(entities, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            _logger.LogInformation("Ingested and saved {Count} compliance chunks.", entities.Count);
            return entities.Count;
        }
    }
}
