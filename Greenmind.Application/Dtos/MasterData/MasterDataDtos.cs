using System.ComponentModel.DataAnnotations;

namespace Greenmind.Application.Dtos.MasterData;

public sealed record MasterDataItemDto(
    int Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt);

public sealed class MasterDataRequest
{
    public MasterDataRequest()
    {
    }

    public MasterDataRequest(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    [Required]
    [MaxLength(100)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; init; }
}

public sealed class MasterDataStatusRequest
{
    public MasterDataStatusRequest()
    {
    }

    public MasterDataStatusRequest(bool? isActive)
    {
        IsActive = isActive;
    }

    [Required]
    public bool? IsActive { get; init; }
}
