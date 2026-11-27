using ErpAssistant.Application.Abstractions;
using ErpAssistant.Application.Common;
using ErpAssistant.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace ErpAssistant.Application.Documents;

public sealed class DocumentService(
    IErpDbContext db,
    IFileStorage storage,
    IDocumentQueue queue,
    IVectorIndex index,
    ICurrentUser user,
    TimeProvider clock)
{
    public const long MaxBytes = 20 * 1024 * 1024;

    public static readonly IReadOnlyDictionary<string, string> AllowedTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".txt"] = "text/plain",
        [".md"] = "text/markdown",
    };

    public Task<PagedResult<DocumentItem>> ListAsync(DocumentQuery q, CancellationToken ct)
    {
        var query = db.Documents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Department))
            query = query.Where(d => d.Department.Code == q.Department);
        if (q.Status is { } status)
            query = query.Where(d => d.Status == status);

        return query.OrderByDescending(d => d.Id).Select(Project()).ToPagedAsync(q, ct);
    }

    public async Task<DocumentItem> GetAsync(int id, CancellationToken ct) =>
        await db.Documents.AsNoTracking().Where(d => d.Id == id).Select(Project()).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Doküman", id);

    public async Task<IReadOnlyList<DepartmentItem>> DepartmentsAsync(CancellationToken ct)
    {
        var allowed = user.DepartmentCodes;
        var all = await db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync(ct);
        return all.Where(d => allowed is null || allowed.Contains(d.Code))
            .Select(d => new DepartmentItem(d.Code, d.Name, CanUpload(d.Code)))
            .ToList();
    }

    public async Task<DocumentItem> UploadAsync(Stream content, string fileName, long size, string departmentCode, CancellationToken ct)
    {
        fileName = Path.GetFileName(fileName);
        var extension = Path.GetExtension(fileName);
        if (!AllowedTypes.TryGetValue(extension, out var contentType))
            throw new FluentValidation.ValidationException($"Desteklenmeyen dosya türü '{extension}'. İzin verilenler: {string.Join(", ", AllowedTypes.Keys)}.");
        if (size is <= 0 or > MaxBytes)
            throw new FluentValidation.ValidationException("Dosya boyutu 1 bayt ile 20 MB arasında olmalı.");

        var department = await db.Departments.FirstOrDefaultAsync(d => d.Code == departmentCode, ct)
            ?? throw new FluentValidation.ValidationException($"Bilinmeyen departman '{departmentCode}'.");
        if (!CanUpload(department.Code))
            throw new ForbiddenException($"{department.Name} departmanına doküman yükleme yetkiniz yok.");

        var path = await storage.SaveAsync(content, fileName, ct);
        var document = new Document
        {
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = size,
            StoragePath = path,
            DepartmentId = department.Id,
            Status = DocumentStatus.Pending,
            UploadedBy = user.UserName,
            UploadedAt = clock.GetUtcNow().UtcDateTime,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync(ct);

        await queue.PublishAsync(document.Id, ct);
        return await GetAsync(document.Id, ct);
    }

    public async Task<DocumentItem> ReprocessAsync(int id, CancellationToken ct)
    {
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Doküman", id);
        document.Status = DocumentStatus.Pending;
        document.Attempts = 0;
        document.Error = null;
        await db.SaveChangesAsync(ct);
        await queue.PublishAsync(id, ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Doküman", id);
        await index.DeleteDocumentAsync(id, ct);
        db.Documents.Remove(document);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(document.StoragePath, ct);
    }

    /// <summary>Yönetici her departmana; diğer roller yalnızca okuyabildikleri departmanlara yükler.</summary>
    private bool CanUpload(string departmentCode) =>
        user.Roles.Contains(Roles.Admin) || (user.DepartmentCodes?.Contains(departmentCode) ?? false);

    private static System.Linq.Expressions.Expression<Func<Document, DocumentItem>> Project() => d => new DocumentItem(
        d.Id, d.FileName, d.ContentType, d.SizeBytes, d.Department.Code, d.Department.Name, d.Status, d.ChunkCount,
        d.Attempts, d.Error, d.UploadedBy, d.UploadedAt, d.ProcessedAt);
}
