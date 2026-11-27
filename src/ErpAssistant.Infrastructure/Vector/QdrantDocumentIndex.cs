using System.Text.RegularExpressions;
using ErpAssistant.Application.Ai;
using ErpAssistant.Application.Documents;
using ErpAssistant.Infrastructure.Ai;
using ErpAssistant.Infrastructure.Documents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using static Qdrant.Client.Grpc.Conditions;

namespace ErpAssistant.Infrastructure.Vector;

public sealed record IndexedChunk(Guid Id, TextChunk Chunk, ReadOnlyMemory<float> Vector);

/// <summary>
/// Qdrant üzerinde doküman parçası indeksi. Her nokta: vektör + payload (documentId, fileName, department,
/// page, chunkIndex, text). Departman alanı keyword index'li; arama yetkili departmanlarla filtrelenir.
/// </summary>
public sealed partial class QdrantDocumentIndex(
    QdrantClient client,
    IOptions<QdrantOptions> options,
    IEmbeddingGeneratorFactory embeddings,
    IEmbeddingGenerator<string, Embedding<float>> generator) : IDocumentSearch, IVectorIndex
{
    private string Collection => $"{options.Value.CollectionPrefix}_{Sanitize().Replace(embeddings.Model, "_")}";

    public async Task EnsureCollectionAsync(int dimensions, CancellationToken ct)
    {
        if (await client.CollectionExistsAsync(Collection, ct)) return;
        await client.CreateCollectionAsync(Collection, new VectorParams { Size = (ulong)dimensions, Distance = Distance.Cosine }, cancellationToken: ct);
        await client.CreatePayloadIndexAsync(Collection, "department", PayloadSchemaType.Keyword, cancellationToken: ct);
        await client.CreatePayloadIndexAsync(Collection, "documentId", PayloadSchemaType.Integer, cancellationToken: ct);
    }

    public async Task UpsertAsync(int documentId, string fileName, string department, IReadOnlyList<IndexedChunk> chunks, CancellationToken ct)
    {
        if (chunks.Count == 0) return;
        await EnsureCollectionAsync(chunks[0].Vector.Length, ct);

        var points = chunks.Select(c => new PointStruct
        {
            Id = c.Id,
            Vectors = c.Vector.ToArray(),
            Payload =
            {
                ["documentId"] = documentId,
                ["fileName"] = fileName,
                ["department"] = department,
                ["page"] = c.Chunk.Page,
                ["chunkIndex"] = c.Chunk.Index,
                ["text"] = c.Chunk.Text,
            },
        }).ToList();

        await client.UpsertAsync(Collection, points, wait: true, cancellationToken: ct);
    }

    public async Task DeleteDocumentAsync(int documentId, CancellationToken ct)
    {
        if (!await client.CollectionExistsAsync(Collection, ct)) return;
        await client.DeleteAsync(Collection, Match("documentId", documentId), wait: true, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<DocumentHit>> SearchAsync(string query, IReadOnlyCollection<string>? departments, int top, CancellationToken ct)
    {
        if (departments is { Count: 0 }) return [];
        if (!await client.CollectionExistsAsync(Collection, ct)) return [];

        var vector = await generator.GenerateVectorAsync(query, cancellationToken: ct);
        Filter? filter = null;
        if (departments is not null) filter = Match("department", departments.ToList());

        var points = await client.SearchAsync(Collection, vector, filter: filter, limit: (ulong)top,
            scoreThreshold: options.Value.MinScore, payloadSelector: true, cancellationToken: ct);
        return points.Select(p => new DocumentHit(
            (int)p.Payload["documentId"].IntegerValue,
            p.Payload["fileName"].StringValue,
            p.Payload["department"].StringValue,
            (int)p.Payload["page"].IntegerValue,
            (int)p.Payload["chunkIndex"].IntegerValue,
            p.Payload["text"].StringValue,
            p.Score)).ToList();
    }

    [GeneratedRegex("[^a-zA-Z0-9]+")]
    private static partial Regex Sanitize();
}
