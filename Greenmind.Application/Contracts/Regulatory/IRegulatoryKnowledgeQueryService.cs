using Greenmind.Application.Dtos.Regulatory;

namespace Greenmind.Application.Contracts.Regulatory;

public interface IRegulatoryKnowledgeQueryService
{
    Task<IReadOnlyList<ActiveSubstanceKnowledgeDto>> FindActiveSubstancesAsync(
        string regionName,
        string nameOrCasOrId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActiveSubstanceKnowledgeDto>> FindActiveSubstancesMentionedAsync(
        string regionName,
        string text,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductMrlKnowledgeDto>> FindProductMrlsAsync(
        string regionName,
        string productType,
        string residueNameOrPesticideId,
        CancellationToken cancellationToken = default);
}
