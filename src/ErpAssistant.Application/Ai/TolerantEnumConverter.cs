using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErpAssistant.Application.Ai;

/// <summary>
/// Araç argümanlarındaki enum değerlerini hoşgörülü okur: büyük/küçük harf ve Türkçe karakter farkı
/// ("Açık" → <c>Acik</c>, "TESLİM EDİLDİ" → <c>TeslimEdildi</c>) tolere edilir. JSON Schema yine geçerli
/// enum değerlerini listeler; bu yalnızca modelin küçük yazım sapmalarını yakalar.
/// </summary>
public sealed class TolerantEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(Converter<>).MakeGenericType(typeToConvert))!;

    /// <summary>Aksanları ve ayraçları atar, küçük harfe çevirir: "TESLİM EDİLDİ" → "teslimedildi".</summary>
    public static string Normalize(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            var c = char.ToLowerInvariant(ch);
            if (c == 'ı') c = 'i';
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        }
        return sb.ToString();
    }

    private sealed class Converter<T> : JsonConverter<T> where T : struct, Enum
    {
        private static readonly Dictionary<string, T> Lookup =
            Enum.GetValues<T>().ToDictionary(v => Normalize(v.ToString()), v => v);

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var n) && Enum.IsDefined(typeof(T), n))
                return (T)(object)n;

            var text = reader.GetString() ?? "";
            return Lookup.TryGetValue(Normalize(text), out var value)
                ? value
                : throw new JsonException($"'{text}' geçerli değil. Geçerli değerler: {string.Join(", ", Enum.GetNames<T>())}.");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }
}
