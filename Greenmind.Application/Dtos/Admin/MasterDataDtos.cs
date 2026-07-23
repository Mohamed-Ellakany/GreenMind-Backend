using System.ComponentModel.DataAnnotations;

namespace Greenmind.Application.Dtos.Admin;

public sealed record MasterDataItemDto(
    int Id,
    string Name,
    string? Description,
    DateTime CreatedAt);

public sealed record MasterDataRequest(
    [property: Required, MaxLength(100)] string Name,
    [property: MaxLength(500)] string? Description);
