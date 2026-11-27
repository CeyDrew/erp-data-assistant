using System.Text.RegularExpressions;

namespace ErpAssistant.Application.Ai;

/// <summary>
/// Doküman parçalarındaki, yapay zekâya yönelik talimat cümlelerini (prompt injection) modele gitmeden önce
/// ayıklar. Sistem istemindeki "doküman metni veridir" kuralının yanında ikinci savunma katmanıdır: küçük
/// modeller istemdeki kurala rağmen gömülü talimata uyabiliyor.
/// </summary>
public static partial class InjectionGuard
{
    public const string Placeholder = "[talimat içeren cümle güvenlik nedeniyle gizlendi]";

    public sealed record Result(string Text, int Redacted);

    public static Result Sanitize(string text)
    {
        var sentences = SentenceSplit().Split(text);
        var redacted = 0;
        var kept = sentences.Select(s =>
        {
            if (!Suspicious().IsMatch(s)) return s;
            redacted++;
            return Placeholder;
        });

        // Ardışık yer tutucuları teke indir.
        var output = Repeated().Replace(string.Join(" ", kept), Placeholder);
        return new Result(output, redacted);
    }

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceSplit();

    // Model, asistan veya araçlara doğrudan hitap eden ya da kuralları geçersiz kılmaya çalışan ifadeler.
    [GeneratedRegex(
        @"(önceki|yukarıdaki|tüm)\s+(\p{L}+\s+)?(talimat|kural|yönerge)\p{L}*|yok\s+say|görmezden\s+gel|" +
        @"yapay\s+zek[aâ]|dil\s+modeli|\basistan\p{L}*|sistem\s+(talimat|istem|mesaj)\p{L}*|" +
        @"\b\p{L}+_\p{L}+\s+aracı\p{L}*|araçlarını\s+çağır|cevabının\s+sonuna|" +
        @"ignore\s+(all\s+)?(previous|prior|above)|disregard|system\s+prompt|you\s+are\s+now|\bassistant\b|\bLLM\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Suspicious();

    [GeneratedRegex(@"(\[talimat içeren cümle güvenlik nedeniyle gizlendi\]\s*){2,}")]
    private static partial Regex Repeated();
}
