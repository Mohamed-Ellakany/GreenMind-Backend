using System.ComponentModel.DataAnnotations;

namespace Greenmind.Api.Models;

public sealed class QualityStandardIngestionRequest
{
    [Required]
    public IFormFile? File { get; init; }

    [Required, MaxLength(100)]
    public string Crop { get; init; } = string.Empty;

    [Required, MaxLength(200)]
    public string SourceName { get; init; } = string.Empty;
}

public sealed class EuRegulationBodyIngestionRequest
{
    [Required]
    public IFormFile? File { get; init; }

    [Required, MaxLength(200)]
    public string SourceName { get; init; } = string.Empty;
}

public sealed class EuRegulationAnnexIngestionRequest
{
    [Required]
    public IFormFile? File { get; init; }

    [Required, MaxLength(200)]
    public string SourceName { get; init; } = string.Empty;

    [Required, MaxLength(200)]
    public string AnnexLabelHint { get; init; } = string.Empty;

    [Required, MaxLength(1000)]
    public string CropKeywordsCsv { get; init; } = string.Empty;
}
