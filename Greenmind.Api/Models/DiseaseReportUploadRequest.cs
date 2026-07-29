using System.ComponentModel.DataAnnotations;

namespace Greenmind.Api.Models;

public sealed class DiseaseReportUploadRequest
{
    [Required]
    public DateOnly ObservationDate { get; init; }

    [MaxLength(500)]
    public string? AffectedArea { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

    [Required]
    public List<IFormFile> Images { get; init; } = [];
}
