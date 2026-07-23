namespace Greenmind.Application.Dtos.Admin;

public sealed record FarmAccountDto(
    int Id,
    string Name,
    string OwnerUserId,
    string OwnerName,
    string? OwnerEmail,
    bool IsApproved,
    DateTime? ApprovedAtUtc,
    bool IsActive,
    DateTime CreatedAt);

public enum FarmAccountStatus
{
    Pending,
    Active,
    Inactive
}
