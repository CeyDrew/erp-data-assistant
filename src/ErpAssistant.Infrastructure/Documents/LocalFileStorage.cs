using ErpAssistant.Application.Documents;
using Microsoft.Extensions.Options;

namespace ErpAssistant.Infrastructure.Documents;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";
    public string Path { get; set; } = "storage/documents";
}

/// <summary>Dosyaları yerel diskte (Docker'da API ve worker arasında paylaşılan volume) tutar.</summary>
internal sealed class LocalFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private string Root => System.IO.Path.GetFullPath(options.Value.Path);

    public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken ct)
    {
        var folder = DateTime.UtcNow.ToString("yyyy-MM");
        var relative = System.IO.Path.Combine(folder, $"{Guid.NewGuid():N}{System.IO.Path.GetExtension(fileName).ToLowerInvariant()}");
        var full = System.IO.Path.Combine(Root, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        await using var file = File.Create(full);
        await content.CopyToAsync(file, ct);
        return relative.Replace('\\', '/');
    }

    public Task<Stream> OpenAsync(string path, CancellationToken ct) => Task.FromResult<Stream>(File.OpenRead(Resolve(path)));

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        var full = Resolve(path);
        if (File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }

    private string Resolve(string relative)
    {
        var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(Root, relative));
        if (!full.StartsWith(Root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Geçersiz dosya yolu.");
        return full;
    }
}
