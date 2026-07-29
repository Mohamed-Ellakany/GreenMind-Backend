using System.ComponentModel.DataAnnotations;

namespace Greenmind.Api.Models;

public sealed class ActiveSubstancesUploadRequest
{
    [Required, MaxLength(200)]
    public string RegionName { get; set; } = string.Empty;

    [Required]
    public IFormFile? File { get; set; }
}

public sealed class ProductMrlsUploadRequest
{
    [Required, MaxLength(200)]
    public string RegionName { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string ProductType { get; set; } = string.Empty;

    [Required]
    public IFormFile? File { get; set; }
}
