using System.Text.Json;
using ErpAssistant.Application.Abstractions;
using ErpAssistant.Domain.Documents;
using ErpAssistant.Infrastructure.Messaging;
using ErpAssistant.Infrastructure.Persistence;
using ErpAssistant.Application.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ErpAssistant.Infrastructure.Documents;

/// <summary>
/// Hiç doküman yoksa <c>samples/documents</c> altındaki örnekleri (manifest.json'daki departmanlarıyla)
/// yükler ve kuyruğa atar. Demo ilk açılışta doküman sorularına cevap verebilsin diye.
/// </summary>
public sealed class SampleDocumentImporter(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<SampleDocumentImporter> logger) : BackgroundService
{
    private sealed record ManifestEntry(string File, string Department);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var folder = configuration["SampleDocuments:Path"];
        if (string.IsNullOrWhiteSpace(folder) || !File.Exists(Path.Combine(folder, "manifest.json"))) return;

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            if (await db.Documents.IgnoreQueryFilters().AnyAsync(ct)) return;

            var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
            var queue = scope.ServiceProvider.GetRequiredService<IDocumentQueue>();
            var manifest = JsonSerializer.Deserialize<List<ManifestEntry>>(
                await File.ReadAllTextAsync(Path.Combine(folder, "manifest.json"), ct), JsonSerializerOptions.Web)!;
            var departments = await db.Departments.ToDictionaryAsync(d => d.Code, ct);

            foreach (var entry in manifest)
            {
                var path = Path.Combine(folder, entry.File);
                await using var file = File.OpenRead(path);
                var stored = await storage.SaveAsync(file, entry.File, ct);
                var doc = new Document
                {
                    FileName = entry.File,
                    ContentType = DocumentService.AllowedTypes[Path.GetExtension(entry.File)],
                    SizeBytes = new FileInfo(path).Length,
                    StoragePath = stored,
                    DepartmentId = departments[entry.Department].Id,
                    Status = DocumentStatus.Pending,
                    UploadedBy = "Örnek veri",
                    UploadedAt = DateTime.UtcNow,
                };
                db.Documents.Add(doc);
                await db.SaveChangesAsync(ct);
                await queue.PublishAsync(doc.Id, ct);
            }
            logger.LogInformation("{Count} örnek doküman kuyruğa alındı", manifest.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Örnek dokümanlar yüklenemedi");
        }
    }
}
