using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Application.Dtos.Admin;

public sealed record AiLimitRequestDto(
    int Id,
    string UserId,
    string UserFullName,
    string? UserEmail,
    string ContactName,
    string ContactPhone,
    decimal CurrentLimitUsd,
    decimal UsedUsd,
    AiLimitRequestStatus Status,
    string? AdminNote,
    decimal? NewLimitUsd,
    DateTime RequestedAtUtc,
    DateTime? ResolvedAtUtc);

public sealed record UserBudgetStatusDto(
    decimal LimitUsd,
    decimal UsedUsd,
    bool IsExceeded,
    bool HasPendingRequest);
