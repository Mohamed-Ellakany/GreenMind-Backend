using System.ComponentModel.DataAnnotations;

namespace Greenmind.Api.Models;

public sealed class DiagnosisMessageUploadRequest
{
    [StringLength(4000)]
    public string? Content { get; init; }

    [Required, StringLength(100, MinimumLength = 8)]
    public string IdempotencyKey { get; init; } = string.Empty;

    public List<IFormFile> Images { get; init; } = [];
}

public sealed class FinalizeDiagnosisApiRequest
{
    public bool Force { get; init; }
}
