using System.Text.RegularExpressions;

namespace Greenmind.Infrastructure.Iti;

public static partial class ItiJson
{
    public static string ExtractObject(string value)
    {
        var cleaned = StripThinkTags(value).Trim();

        // Check if there is a markdown code block anywhere in the string
        var fenceStart = cleaned.IndexOf("```", StringComparison.Ordinal);
        if (fenceStart >= 0)
        {
            var afterFence = cleaned.IndexOf('\n', fenceStart);
            var fenceEnd = cleaned.LastIndexOf("```", StringComparison.Ordinal);
            if (afterFence >= 0 && fenceEnd > afterFence)
            {
                var blockContent = cleaned[(afterFence + 1)..fenceEnd].Trim();
                var bStart = blockContent.IndexOf('{');
                var bEnd = blockContent.LastIndexOf('}');
                if (bStart >= 0 && bEnd >= bStart)
                    return blockContent[bStart..(bEnd + 1)];
            }
        }

        var start = cleaned.IndexOf('{');
        var end = cleaned.LastIndexOf('}');
        if (start < 0 || end < start)
            throw new InvalidDataException("ITI model response did not contain a JSON object.");

        return cleaned[start..(end + 1)];
    }

    public static string StripThinkTags(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return ThinkTagRegex().Replace(value, string.Empty).Trim();
    }

    [GeneratedRegex(@"<think>[\s\S]*?</think>", RegexOptions.IgnoreCase)]
    private static partial Regex ThinkTagRegex();
}

