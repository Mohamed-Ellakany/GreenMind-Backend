using Greenmind.Application.Dtos.Regulatory;

namespace Greenmind.Application.Contracts.Regulatory;

public interface IRegulatoryDataImportService
{
    Task<RegulatoryImportResultDto> ImportActiveSubstancesAsync(
        string regionName,
        string fileName,
        long fileLength,
        Stream fileStream,
        CancellationToken cancellationToken = default);

    Task<RegulatoryImportResultDto> ImportProductMrlsAsync(
        string regionName,
        string productType,
        string fileName,
        long fileLength,
        Stream fileStream,
        CancellationToken cancellationToken = default);
}
