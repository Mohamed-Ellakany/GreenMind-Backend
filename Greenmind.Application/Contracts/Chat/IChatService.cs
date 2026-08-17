using Greenmind.Application.Dtos.Chat;
using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Application.Contracts.Chat;

public interface IChatService
{
    Task<ChatOperationResult<ChatSessionDto>> CreatePlanSessionAsync(
        int exportPlanId,
        string ownerUserId,
        CreateChatSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatSessionDto>> ListPlanSessionsAsync(
        int exportPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<ChatOperationResult<ChatSessionDto>> CreateFarmSessionAsync(
        string ownerUserId,
        CreateChatSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatSessionDto>> ListFarmSessionsAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<ChatOperationResult<ChatHistoryPageDto>> GetMessagesAsync(
        ChatScope scope,
        int? exportPlanId,
        int sessionId,
        string ownerUserId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ChatOperationResult<ChatStreamHandle>> StartReplyAsync(
        ChatScope scope,
        int? exportPlanId,
        int sessionId,
        string ownerUserId,
        SendChatMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<ChatOperationResult<ChatAttachmentDownload>> DownloadAttachmentAsync(
        ChatScope scope,
        int? exportPlanId,
        int sessionId,
        int attachmentId,
        string ownerUserId,
        CancellationToken cancellationToken = default);
}

public sealed record ChatStreamHandle(IAsyncEnumerable<ChatStreamEventDto> Events);

public enum ChatOperationStatus
{
    Succeeded,
    NotFound,
    Invalid
}

public sealed record ChatOperationResult<T>(
    ChatOperationStatus Status,
    T? Value = default,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    public static ChatOperationResult<T> Success(T value) =>
        new(ChatOperationStatus.Succeeded, value);

    public static ChatOperationResult<T> NotFound() =>
        new(ChatOperationStatus.NotFound);

    public static ChatOperationResult<T> Invalid(string code, string message) =>
        new(ChatOperationStatus.Invalid, default, code, message);
}
