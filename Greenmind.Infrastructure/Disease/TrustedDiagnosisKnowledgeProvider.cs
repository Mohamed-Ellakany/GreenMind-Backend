using System.Net;
using System.Text.RegularExpressions;
using Greenmind.Application.Interfaces.AI;

namespace Greenmind.Infrastructure.Disease;

public sealed partial class TrustedDiagnosisKnowledgeProvider(
    HttpClient httpClient,
    TimeProvider timeProvider) : IDiagnosisKnowledgeProvider
{
    private const string StrawberryIndex = "https://ipm.ucanr.edu/agriculture/strawberry/";

    public async Task<DiagnosisKnowledgeResult> SearchAsync(
        string crop,
        string diseaseSearchTerm,
        CancellationToken cancellationToken = default)
    {
        if (!crop.Equals("Strawberry", StringComparison.OrdinalIgnoreCase))
            return new DiagnosisKnowledgeResult([], "البحث الموثوق المباشر متاح للفراولة فقط في النسخة الحالية.");

        try
        {
            var indexHtml = await httpClient.GetStringAsync(StrawberryIndex, cancellationToken);
            var path = FindBestDiseasePath(indexHtml, diseaseSearchTerm);
            var sourceUrl = path is null
                ? StrawberryIndex
                : new Uri(new Uri(StrawberryIndex), path).AbsoluteUri;
            var html = path is null
                ? indexHtml
                : await httpClient.GetStringAsync(sourceUrl, cancellationToken);
            var text = CleanHtml(html);
            if (text.Length < 100)
                return new DiagnosisKnowledgeResult([], "المصدر الأكاديمي لم يُرجع محتوى كافيًا.");

            var excerpt = text.Length <= 1800 ? text : text[..1800];
            var source = new DiagnosisKnowledgeSource(
                "UC IPM Pest Management Guidelines: Strawberry",
                sourceUrl,
                "University of California Statewide IPM Program",
                diseaseSearchTerm,
                excerpt,
                timeProvider.GetUtcNow().UtcDateTime,
                true);
            return new DiagnosisKnowledgeResult([source], null);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new DiagnosisKnowledgeResult([], "تعذر الوصول إلى المصدر الأكاديمي الموثوق وقت التشخيص.");
        }
    }

    private static string? FindBestDiseasePath(string html, string term)
    {
        var aliases = DiseaseAliases(term);
        var matches = LinkRegex().Matches(html);
        return matches
            .Select(match => new
            {
                Href = WebUtility.HtmlDecode(match.Groups["href"].Value),
                Text = CleanHtml(match.Groups["text"].Value)
            })
            .Where(item => item.Href.Contains("/agriculture/strawberry/", StringComparison.OrdinalIgnoreCase))
            .Select(item => new
            {
                item.Href,
                Score = aliases.Count(alias => item.Text.Contains(alias, StringComparison.OrdinalIgnoreCase) ||
                                               item.Href.Contains(Slug(alias), StringComparison.OrdinalIgnoreCase))
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .Select(item => item.Href)
            .FirstOrDefault();
    }

    private static string[] DiseaseAliases(string term)
    {
        var values = new List<string> { term };
        var normalized = term.ToLowerInvariant();
        if (normalized.Contains("عفن رمادي") || normalized.Contains("botrytis")) values.AddRange(["Botrytis Fruit Rot", "gray mold"]);
        if (normalized.Contains("بياض دقيقي") || normalized.Contains("powdery")) values.Add("Powdery Mildew");
        if (normalized.Contains("أنثراكنوز") || normalized.Contains("انثراكنوز") || normalized.Contains("anthracnose")) values.Add("Anthracnose");
        if (normalized.Contains("فيوزاريوم") || normalized.Contains("fusarium")) values.Add("Fusarium Wilt");
        if (normalized.Contains("فيتوفثورا") || normalized.Contains("phytophthora")) values.Add("Phytophthora Crown and Root Rot");
        if (normalized.Contains("تبقع") || normalized.Contains("leaf spot")) values.Add("Common Leaf Spot");
        return values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string Slug(string value) => Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    private static string CleanHtml(string html)
    {
        var withoutScripts = ScriptRegex().Replace(html, " ");
        var withoutTags = TagRegex().Replace(withoutScripts, " ");
        return WhitespaceRegex().Replace(WebUtility.HtmlDecode(withoutTags), " ").Trim();
    }

    [GeneratedRegex("<a[^>]+href=[\\\"'](?<href>[^\\\"']+)[\\\"'][^>]*>(?<text>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LinkRegex();

    [GeneratedRegex("<(script|style)[^>]*>.*?</\\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex TagRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
