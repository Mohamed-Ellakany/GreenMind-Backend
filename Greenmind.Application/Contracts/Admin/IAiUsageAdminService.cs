using Greenmind.Application.Dtos.Admin;

namespace Greenmind.Application.Contracts.Admin;

public interface IAiUsageAdminService
{
    Task<AdminAiUsageOverviewDto> GetOverviewAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AdminUserDetailsDto?> GetUserDetailsAsync(
        string userId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<bool> SetAiAccessSuspendedAsync(
        string userId,
        bool isSuspended,
        CancellationToken cancellationToken = default);
}
