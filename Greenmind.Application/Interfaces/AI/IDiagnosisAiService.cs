using Greenmind.Application.Dtos.Disease;
using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Application.Interfaces.AI;

public interface IDiagnosisConversationAiService
{
    Task<DiagnosisConversationTurn> ReplyAsync(
        DiagnosisConversationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record DiagnosisConversationRequest(
    string PlanContextJson,
    string CurrentMessage,
    DiagnosisFactsDto CurrentFacts,
    IReadOnlyList<ConversationHistoryItem> RecentMessages,
    IReadOnlyList<ConversationImage> CurrentImages,
    int TotalImageCount,
    string? OwnerUserId = null,
    int? ExportPlanId = null);

public sealed record DiagnosisConversationTurn(
    string AssistantText,
    DiagnosisFactsDto Facts);

public interface IDiagnosisKnowledgeProvider
{
    Task<DiagnosisKnowledgeResult> SearchAsync(
        string cropCanonicalKey,
        string diseaseSearchTerm,
        CancellationToken cancellationToken = default);
}

public sealed record DiagnosisKnowledgeSource(
    string SourceName,
    string SourceUrl,
    string? Publisher,
    string? SectionReference,
    string Excerpt,
    DateTime RetrievedAtUtc,
    bool IsOfficialOrAcademic);

public sealed record DiagnosisKnowledgeResult(
    IReadOnlyList<DiagnosisKnowledgeSource> Sources,
    string? FailureMessage)
{
    public bool IsVerified => Sources.Any(source => source.IsOfficialOrAcademic);
    public string Context => string.Join("\n\n", Sources.Select(source =>
        $"[{source.SourceName}{(string.IsNullOrWhiteSpace(source.SectionReference) ? string.Empty : " - " + source.SectionReference)}]\n{source.Excerpt}"));
}
