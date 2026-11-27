using ErpAssistant.Application.Common;
using ErpAssistant.Domain.Documents;

namespace ErpAssistant.Application.Documents;

public sealed record DocumentQuery : PageQuery
{
    public string? Department { get; init; }
    public DocumentStatus? Status { get; init; }
}

public sealed class DocumentQueryValidator : PageQueryValidator<DocumentQuery>;

public sealed record DocumentItem(
    int Id, string FileName, string ContentType, long SizeBytes, string DepartmentCode, string DepartmentName,
    DocumentStatus Status, int ChunkCount, int Attempts, string? Error, string? UploadedBy, DateTime UploadedAt,
    DateTime? ProcessedAt);

public sealed record DepartmentItem(string Code, string Name, bool CanUpload);
