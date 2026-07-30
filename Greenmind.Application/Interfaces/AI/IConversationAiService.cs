using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Application.Interfaces.AI;

public interface IConversationAiService
{
    IAsyncEnumerable<string> StreamReplyAsync(
        ConversationAiRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ConversationAiRequest(
    ChatScope Scope,
    string SessionTitle,
    string UserMessage,
    string ContextJson,
    string? ConversationSummary,
    IReadOnlyList<ConversationHistoryItem> RecentMessages);

public sealed record ConversationHistoryItem(
    ChatMessageRole Role,
    string Content);
