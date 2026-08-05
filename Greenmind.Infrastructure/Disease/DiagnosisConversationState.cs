using System.Text.RegularExpressions;
using Greenmind.Application.Dtos.Disease;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Domain.Consts.Enums;

namespace Greenmind.Infrastructure.Disease;

internal static partial class DiagnosisConversationState
{
    private static readonly IReadOnlyDictionary<string, string> FollowUpQuestions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["image"] = "ارفع صورة واضحة وقريبة للجزء المصاب في إضاءة جيدة.",
            ["affectedPart"] = "أي جزء من النبات ظاهر عليه الأعراض بالضبط: الثمار، الأوراق، الساق ولا الجذور؟",
            ["onset"] = "إمتى ظهرت الأعراض أول مرة، من كام يوم تقريبًا؟",
            ["spreadPattern"] = "الأعراض محدودة في مكان واحد ولا منتشرة في أكتر من منطقة؟",
            ["affectedExtent"] = "تقريبًا كام في المية من النباتات أو الثمار متأثر؟",
            ["conditions"] = "قبل ظهور الأعراض، كان فيه تغيير في الري أو الرطوبة أو الحرارة أو التهوية؟",
            ["previousTreatments"] = "هل استخدمت أي مبيد أو علاج على الفراولة قبل ظهور الأعراض؟"
        };

    public static DiagnosisFactsDto RecoverFacts(
        DiagnosisFactsDto current,
        IReadOnlyList<ConversationHistoryItem> history,
        string currentMessage)
    {
        var previousUserMessages = history
            .Where(item => item.Role == ChatMessageRole.User)
            .Select(item => item.Content)
            .Where(content => !string.IsNullOrWhiteSpace(content));
        var messages = previousUserMessages
            .Append(currentMessage)
            .Where(content => !string.IsNullOrWhiteSpace(content))
            .Select(content => new Candidate(content.Trim(), Normalize(content)))
            .ToList();

        var affectedPart = current.AffectedPart ?? Latest(messages, HasAffectedPart);
        var onset = current.Onset ?? Latest(messages, HasOnset);
        var spreadPattern = current.SpreadPattern ?? Latest(messages, HasSpreadPattern);
        var affectedExtent = current.AffectedExtent ?? Latest(messages, HasAffectedExtent);
        var conditions = current.Conditions ?? Latest(messages, HasConditions);
        var previousTreatments = current.PreviousTreatments ?? Latest(messages, HasTreatmentDetails);

        var lastAssistant = history.LastOrDefault(item => item.Role == ChatMessageRole.Assistant)?.Content;
        var askedKey = InferSingleAskedKey(lastAssistant);
        if (askedKey is not null && IsUsableDirectAnswer(currentMessage, askedKey))
        {
            var answer = currentMessage.Trim();
            switch (askedKey)
            {
                case "affectedPart": affectedPart ??= answer; break;
                case "onset": onset ??= answer; break;
                case "spreadPattern": spreadPattern ??= answer; break;
                case "affectedExtent": affectedExtent ??= answer; break;
                case "conditions": conditions ??= answer; break;
                case "previousTreatments": previousTreatments ??= NormalizeNegativeTreatment(answer); break;
            }
        }

        if (previousTreatments is null && EndsWithNo(currentMessage) &&
            lastAssistant is not null && HasTreatmentDetails(new Candidate(lastAssistant, Normalize(lastAssistant))))
        {
            previousTreatments = "لا توجد مبيدات أو علاجات سابقة";
        }

        return new DiagnosisFactsDto(
            affectedPart, onset, spreadPattern, affectedExtent, conditions, previousTreatments);
    }

    public static string BuildAssistantReply(
        string modelText,
        DiagnosisReadinessDto readiness,
        bool includeImageObservation)
    {
        var acknowledgement = includeImageObservation
            ? ExtractObservation(modelText)
            : "تمام، سجلت إجابتك.";

        if (readiness.IsReady)
        {
            return $"{acknowledgement}\n\nكده المعلومات الأساسية مكتملة. راجعها واضغط «تشخيص» لما تكون جاهز.";
        }

        var nextMissing = readiness.Checklist.FirstOrDefault(item => item.IsRequired && !item.IsComplete);
        if (nextMissing is null || !FollowUpQuestions.TryGetValue(nextMissing.Key, out var question))
            return acknowledgement;

        return $"{acknowledgement}\n\n{question}";
    }

    private static string? Latest(IEnumerable<Candidate> messages, Func<Candidate, bool> predicate) =>
        messages.LastOrDefault(predicate)?.Original;

    private static bool HasAffectedPart(Candidate candidate) => ContainsAny(candidate.Normalized,
        "ثمر", "فراول", "ورق", "اوراق", "ورقه", "ساق", "جذر", "جذور", "تاج", "زهر", "نبات");

    private static bool HasOnset(Candidate candidate) =>
        OnsetDurationRegex().IsMatch(candidate.Normalized) || ContainsAny(candidate.Normalized,
            "امبارح", "اول امبارح", "النهارده", "منذ", "ظهرت من", "بدات من", "اول مره");

    private static bool HasSpreadPattern(Candidate candidate) => ContainsAny(candidate.Normalized,
        "محدود", "منتشر", "متمركز", "مكان واحد", "منطقه واحده", "اكتر من منطقه", "باقي الحقل", "كل الحقل");

    private static bool HasAffectedExtent(Candidate candidate) =>
        PercentageRegex().IsMatch(candidate.Normalized) || ContainsAny(candidate.Normalized,
            "في الميه", "نصف", "نص ", "ربع", "تلت", "ثلث", "معظم", "اغلب");

    private static bool HasConditions(Candidate candidate) => ContainsAny(candidate.Normalized,
        "طقس", "الجو", "حراره", "رطوب", "مطر", "ري ", "الري", "مياه", "تهويه", "صوبه");

    private static bool HasTreatmentDetails(Candidate candidate) => ContainsAny(candidate.Normalized,
        "مبيد", "علاج", "رش", "فطري", "كيماوي", "معامله", "اجراء");

    private static string? InferSingleAskedKey(string? assistantText)
    {
        if (string.IsNullOrWhiteSpace(assistantText)) return null;
        var normalized = Normalize(assistantText);
        var keys = new List<string>();
        if (ContainsAny(normalized, "اي جزء من النبات ظاهر")) keys.Add("affectedPart");
        if (ContainsAny(normalized, "امتي ظهرت الاعراض اول مره")) keys.Add("onset");
        if (ContainsAny(normalized, "الاعراض محدوده في مكان واحد")) keys.Add("spreadPattern");
        if (ContainsAny(normalized, "كام في الميه من النباتات")) keys.Add("affectedExtent");
        if (ContainsAny(normalized, "قبل ظهور الاعراض، كان فيه تغيير")) keys.Add("conditions");
        if (ContainsAny(normalized, "هل استخدمت اي مبيد او علاج")) keys.Add("previousTreatments");
        return keys.Count == 1 ? keys[0] : null;
    }

    private static bool IsUsableDirectAnswer(string content, string key)
    {
        var normalized = Normalize(content).Trim();
        if (normalized.Length == 0 || ContainsAny(normalized, "مش عارف", "لا اعرف", "مش متاكد")) return false;
        if (key is "conditions" or "previousTreatments") return true;
        return normalized is not "لا" and not "لأ" and not "لا يوجد";
    }

    private static string NormalizeNegativeTreatment(string answer) =>
        Normalize(answer).Trim() is "لا" or "لأ" or "لا يوجد" or "مافيش"
            ? "لا توجد مبيدات أو علاجات سابقة"
            : answer;

    private static bool EndsWithNo(string content)
    {
        var lines = Normalize(content).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length > 0 && lines[^1] is "لا" or "لأ" or "مافيش";
    }

    private static string ExtractObservation(string modelText)
    {
        var lines = modelText.Replace("\r", string.Empty).Split('\n');
        var kept = lines
            .TakeWhile(line => !line.Contains('؟') &&
                !ContainsAny(Normalize(line), "سؤال", "محتاجين", "محتاج اعرف", "من فضلك"))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim())
            .ToList();
        var observation = string.Join("\n", kept).Trim();
        return string.IsNullOrWhiteSpace(observation) ? "تم استلام الصورة وتسجيلها." : observation;
    }

    private static string Normalize(string value) => value
        .ToLowerInvariant()
        .Replace('أ', 'ا')
        .Replace('إ', 'ا')
        .Replace('آ', 'ا')
        .Replace('ى', 'ي')
        .Replace('ة', 'ه');

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.Ordinal));

    private sealed record Candidate(string Original, string Normalized);

    [GeneratedRegex(@"(?:من|بقال(?:ها)?|ظهرت|بدأت|بدات)\s*(?:حوالي\s*)?(?:\d+|يومين|يوم|اسبوع|اسبوعين|شهر)\s*(?:يوم|ايام|اسبوع|اسابيع|شهر)?")]
    private static partial Regex OnsetDurationRegex();

    [GeneratedRegex(@"(?:%\s*\d+(?:[.,]\d+)?|\d+(?:[.,]\d+)?\s*%)")]
    private static partial Regex PercentageRegex();
}
