using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Greenmind.Api.Json;

public sealed class ArabicBusinessEnumJsonConverterFactory : JsonConverterFactory
{
    private static readonly HashSet<Type> SupportedTypes =
    [
        typeof(ChatScope),
        typeof(ChatMessageRole),
        typeof(ChatMessageStatus),
        typeof(TaskChangeProposalStatus),
        typeof(TaskChangeOperation),
        typeof(DiseaseAnalysisStatus),
        typeof(DiseaseSeverity),
        typeof(ExpensePaymentMethod),
        typeof(ExpenseAttachmentType)
    ];

    public override bool CanConvert(Type typeToConvert)
    {
        var underlyingType = Nullable.GetUnderlyingType(typeToConvert);
        return SupportedTypes.Contains(underlyingType ?? typeToConvert);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var underlyingType = Nullable.GetUnderlyingType(typeToConvert);
        var converterType = underlyingType is null
            ? typeof(ArabicBusinessEnumJsonConverter<>).MakeGenericType(typeToConvert)
            : typeof(NullableArabicBusinessEnumJsonConverter<>).MakeGenericType(underlyingType);

        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class ArabicBusinessEnumJsonConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String ||
                !ArabicBusinessValues.TryParseArabic(reader.GetString(), out TEnum value))
            {
                throw new JsonException($"قيمة {typeof(TEnum).Name} غير صالحة.");
            }

            return value;
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToArabic());
    }

    private sealed class NullableArabicBusinessEnumJsonConverter<TEnum> : JsonConverter<TEnum?>
        where TEnum : struct, Enum
    {
        public override TEnum? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.String ||
                !ArabicBusinessValues.TryParseArabic(reader.GetString(), out TEnum value))
            {
                throw new JsonException($"قيمة {typeof(TEnum).Name} غير صالحة.");
            }

            return value;
        }

        public override void Write(Utf8JsonWriter writer, TEnum? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(value.Value.ToArabic());
        }
    }
}
