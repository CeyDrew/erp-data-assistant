using System.Diagnostics;
using ErpAssistant.Application.Documents;
using ErpAssistant.Domain.Documents;
using ErpAssistant.Infrastructure.Persistence;
using ErpAssistant.Infrastructure.Vector;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ErpAssistant.Infrastructure.Documents;

/// <summary>Ayıkla → parçala → embedding → Qdrant → SQL metadata → durum "Hazır".</summary>
public sealed class DocumentProcessor(
    ErpDbContext db,
    IFileStorage storage,
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    QdrantDocumentIndex index,
    TimeProvider clock,
    ILogger<DocumentProcessor> logger)
{
    private const int EmbeddingBatch = 16;

    public async Task ProcessAsync(int documentId, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var document = await db.Documents.Include(d => d.Department).FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (document is null)
        {
            logger.LogWarning("Doküman {Id} bulunamadı (silinmiş olabilir); mesaj atlanıyor", documentId);
            return;
        }

        document.StartProcessing();
        await db.SaveChangesAsync(ct);

        IReadOnlyList<PageText> pages;
        await using (var stream = await storage.OpenAsync(document.StoragePath, ct))
        {
            // PdfPig ve OpenXml arama yapabilen akış ister.
            var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            pages = TextExtractor.Extract(buffer, document.FileName);
        }
        if (pages.Count == 0)
            throw new InvalidOperationException("Dokümandan metin çıkarılamadı (taranmış görüntü olabilir).");

        var chunks = TextChunker.Chunk(pages);

        var indexed = new List<IndexedChunk>(chunks.Count);
        foreach (var batch in chunks.Chunk(EmbeddingBatch))
        {
            var vectors = await embeddings.GenerateAsync(batch.Select(c => c.Text), cancellationToken: ct);
            indexed.AddRange(batch.Zip(vectors, (c, v) => new IndexedChunk(Guid.NewGuid(), c, v.Vector)));
        }

        // Yeniden işlemede eski parçalar temizlenir (idempotent).
        await index.DeleteDocumentAsync(document.Id, ct);
        await index.UpsertAsync(document.Id, document.FileName, document.Department.Code, indexed, ct);

        await db.DocumentChunks.Where(c => c.DocumentId == document.Id).ExecuteDeleteAsync(ct);
        db.DocumentChunks.AddRange(indexed.Select(c => new DocumentChunk
        {
            DocumentId = document.Id,
            ChunkIndex = c.Chunk.Index,
            PageNumber = c.Chunk.Page,
            TokenCount = c.Chunk.TokenCount,
            VectorId = c.Id,
        }));
        document.MarkReady(indexed.Count, clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Doküman {Id} ({File}) işlendi: {Pages} sayfa, {Chunks} parça, {Ms} ms",
            document.Id, document.FileName, pages.Count, indexed.Count, sw.ElapsedMilliseconds);
    }

    public async Task MarkFailedAsync(int documentId, string error, CancellationToken ct)
    {
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (document is null) return;
        document.MarkFailed(error);
        await db.SaveChangesAsync(ct);
    }
}
