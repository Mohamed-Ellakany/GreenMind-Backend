namespace Greenmind.Infrastructure.Iti;

public static class ItiJson
{
    public static string ExtractObject(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = trimmed.IndexOf('\n');
            var closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewLine >= 0 && closingFence > firstNewLine)
                trimmed = trimmed[(firstNewLine + 1)..closingFence].Trim();
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end < start)
            throw new InvalidDataException("ITI model response did not contain a JSON object.");

        return trimmed[start..(end + 1)];
    }
}
