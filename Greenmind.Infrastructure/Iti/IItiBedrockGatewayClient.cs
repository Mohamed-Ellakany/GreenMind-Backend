namespace Greenmind.Infrastructure.Iti;

public interface IItiBedrockGatewayClient
{
    Task<string> ChatAsync(
        string? systemPrompt,
        IReadOnlyList<ItiChatMessage> messages,
        string? modelId = null,
        CancellationToken cancellationToken = default);

    Task<string> MultimodalChatAsync(
        string? systemPrompt,
        string text,
        IReadOnlyList<ItiImageInput> images,
        int maxTokens = 1200,
        string? modelId = null,
        CancellationToken cancellationToken = default);

}

public sealed record ItiChatMessage(string Role, string Content);

public sealed record ItiImageInput(string Format, string DataBase64);
