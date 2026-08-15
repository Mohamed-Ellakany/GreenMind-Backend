using Greenmind.Application.Dtos.Admin;

namespace Greenmind.Application.Contracts.Admin;

public interface IAiLimitRequestService
{
    Task<UserBudgetStatusDto> GetUserBudgetStatusAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> SubmitRequestAsync(
        string userId,
        string contactName,
        string contactPhone,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiLimitRequestDto>> GetRequestsAsync(
        bool pendingOnly = true,
        CancellationToken cancellationToken = default);

    Task<bool> ResolveRequestAsync(
        int requestId,
        string adminUserId,
        bool approved,
        decimal? newLimitUsd,
        string? adminNote,
        CancellationToken cancellationToken = default);
}
