using System.Globalization;
using System.Text.RegularExpressions;
using ErpAssistant.Infrastructure.Ai;
using Microsoft.Extensions.AI;

namespace ErpAssistant.IntegrationTests.Infrastructure;

/// <summary>
/// Deterministik sahte embedding: kelime kökü (ilk 5 harf) özetlerinden 256 boyutlu, normalize vektör.
/// Ortak kelimesi olan metinler benzer çıkar; testlerde gerçek modele gerek kalmaz.
/// </summary>
public sealed partial class HashingEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private const int Dimensions = 256;
    private static readonly CultureInfo Tr = new("tr-TR");

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = new GeneratedEmbeddings<Embedding<float>>();
        foreach (var value in values)
        {
            var vector = new float[Dimensions];
            foreach (Match m in Word().Matches(value.ToLower(Tr)))
            {
                var stem = m.Value.Length > 5 ? m.Value[..5] : m.Value;
                vector[(int)((uint)StableHash(stem) % Dimensions)] += 1;
            }
            var norm = MathF.Sqrt(vector.Sum(v => v * v));
            if (norm > 0) for (var i = 0; i < vector.Length; i++) vector[i] /= norm;
            result.Add(new Embedding<float>(vector));
        }
        return Task.FromResult(result);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }

    private static int StableHash(string s)
    {
        unchecked
        {
            var h = 23;
            foreach (var c in s) h = h * 31 + c;
            return h;
        }
    }

    [GeneratedRegex(@"\p{L}{3,}")]
    private static partial Regex Word();
}

public sealed class HashingEmbeddingFactory : IEmbeddingGeneratorFactory
{
    public string Model => "hashing-256";
    public IEmbeddingGenerator<string, Embedding<float>> Create() => new HashingEmbeddingGenerator();
}
