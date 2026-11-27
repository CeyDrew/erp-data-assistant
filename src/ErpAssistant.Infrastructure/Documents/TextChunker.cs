using System.Text;
using System.Text.RegularExpressions;

namespace ErpAssistant.Infrastructure.Documents;

public sealed record TextChunk(int Index, int Page, string Text, int TokenCount);

/// <summary>
/// Metni yaklaşık token sınırına göre örtüşen parçalara böler. Parçalar sayfa sınırını aşmaz (kaynak
/// gösterimi tek sayfaya işaret etsin diye). Token sayısı yaklaşık: Türkçe metinde ≈ 4 karakter/token.
/// </summary>
public static partial class TextChunker
{
    public const int CharsPerToken = 4;

    public static IReadOnlyList<TextChunk> Chunk(IEnumerable<PageText> pages, int maxTokens = 800, int overlapTokens = 100)
    {
        var maxChars = maxTokens * CharsPerToken;
        var overlapChars = overlapTokens * CharsPerToken;
        var chunks = new List<TextChunk>();

        foreach (var page in pages)
        {
            var sentences = SentenceSplit().Split(Normalize(page.Text)).Where(s => s.Length > 0).ToList();
            var current = new StringBuilder();

            foreach (var sentence in sentences.SelectMany(s => s.Length > maxChars ? HardSplit(s, maxChars) : [s]))
            {
                if (current.Length > 0 && current.Length + sentence.Length + 1 > maxChars)
                {
                    Add(current.ToString());
                    var tail = current.ToString();
                    current.Clear();
                    // Örtüşme: önceki parçanın son kısmı (kelime sınırından) yeni parçanın başına.
                    if (overlapChars > 0 && tail.Length > overlapChars)
                    {
                        var start = tail.IndexOf(' ', tail.Length - overlapChars);
                        current.Append(tail[(start < 0 ? tail.Length - overlapChars : start + 1)..]).Append(' ');
                    }
                }
                current.Append(sentence).Append(' ');
            }
            if (current.ToString().Trim().Length > 0) Add(current.ToString());

            void Add(string text)
            {
                text = text.Trim();
                chunks.Add(new TextChunk(chunks.Count, page.Page, text, Math.Max(1, text.Length / CharsPerToken)));
            }
        }
        return chunks;
    }

    private static string Normalize(string text) => Whitespace().Replace(text.Replace("\r", ""), " ").Trim();

    private static IEnumerable<string> HardSplit(string s, int size)
    {
        for (var i = 0; i < s.Length; i += size) yield return s.Substring(i, Math.Min(size, s.Length - i));
    }

    [GeneratedRegex(@"(?<=[.!?:;])\s+|\n{2,}")]
    private static partial Regex SentenceSplit();

    [GeneratedRegex(@"[ \t\f\v]+")]
    private static partial Regex Whitespace();
}
