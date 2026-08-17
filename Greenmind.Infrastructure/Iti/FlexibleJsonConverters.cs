using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Greenmind.Infrastructure.Iti;

/// <summary>
/// A resilient string converter for LLM JSON outputs where string properties
/// may occasionally be emitted as numbers, booleans, or complex structures.
/// </summary>
public sealed class FlexibleStringJsonConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();

            case JsonTokenType.Number:
                if (reader.TryGetInt64(out var longVal))
                    return longVal.ToString(CultureInfo.InvariantCulture);
                if (reader.TryGetDouble(out var doubleVal))
                    return doubleVal.ToString(CultureInfo.InvariantCulture);
                return reader.GetDecimal().ToString(CultureInfo.InvariantCulture);

            case JsonTokenType.True:
                return "true";

            case JsonTokenType.False:
                return "false";

            case JsonTokenType.Null:
                return null;

            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                using (var doc = JsonDocument.ParseValue(ref reader))
                {
                    return doc.RootElement.GetRawText();
                }

            default:
                return reader.GetString();
        }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

/// <summary>
/// A resilient DateOnly converter for LLM JSON outputs supporting various standard date representations.
/// </summary>
public sealed class FlexibleDateOnlyJsonConverter : JsonConverter<DateOnly>
{
    private static readonly string[] Formats = ["yyyy-MM-dd", "yyyy/MM/dd", "yyyy-M-d", "yyyy/M/d", "MM/dd/yyyy", "dd-MM-yyyy", "dd/MM/yyyy"];

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return default;

        var raw = reader.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return default;

        if (DateOnly.TryParseExact(raw, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
            return DateOnly.FromDateTime(dateTime);

        if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fallback))
            return fallback;

        return DateOnly.ParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}
