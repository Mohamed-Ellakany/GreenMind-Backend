using Greenmind.Application.Dtos.Support;

namespace Greenmind.Application.Contracts.Support;

public interface ISupportService
{
    Task<SupportOperationResult<SupportConversationDto>> GetOrCreateCustomerConversationAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupportConversationDto>> ListAdminConversationsAsync(
        CancellationToken cancellationToken = default);

    Task<SupportOperationResult<SupportMessagePageDto>> GetCustomerMessagesAsync(
        string ownerUserId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<SupportOperationResult<SupportMessagePageDto>> GetAdminMessagesAsync(
        int conversationId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<SupportOperationResult<SupportMessageDto>> SendCustomerMessageAsync(
        string ownerUserId,
        SendSupportMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<SupportOperationResult<SupportMessageDto>> SendAdminMessageAsync(
        int conversationId,
        string adminUserId,
        SendSupportMessageRequest request,
        CancellationToken cancellationToken = default);
}

public enum SupportOperationStatus
{
    Succeeded,
    NotFound,
    Invalid
}

public sealed record SupportOperationResult<T>(
    SupportOperationStatus Status,
    T? Value = default,
    string? ErrorMessage = null)
{
    public static SupportOperationResult<T> Success(T value) =>
        new(SupportOperationStatus.Succeeded, value);

    public static SupportOperationResult<T> NotFound() =>
        new(SupportOperationStatus.NotFound);

    public static SupportOperationResult<T> Invalid(string message) =>
        new(SupportOperationStatus.Invalid, default, message);
}
