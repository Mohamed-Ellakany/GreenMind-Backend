using System.ComponentModel.DataAnnotations;

namespace Greenmind.Api.Models;

public sealed class ExpenseAttachmentUploadRequest
{
    [Required]
    public string AttachmentType { get; init; } = string.Empty;

    [Required]
    public IFormFile? File { get; init; }
}
