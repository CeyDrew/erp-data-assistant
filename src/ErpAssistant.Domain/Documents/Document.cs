using ErpAssistant.Domain.Common;
using ErpAssistant.Domain.Organization;

namespace ErpAssistant.Domain.Documents;

public sealed class Document : Entity
{
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string StoragePath { get; set; }
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public DocumentStatus Status { get; set; }
    public int ChunkCount { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }
    public string? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    public List<DocumentChunk> Chunks { get; set; } = [];

    public void StartProcessing()
    {
        Status = DocumentStatus.Processing;
        Attempts++;
        Error = null;
    }

    public void MarkReady(int chunkCount, DateTime processedAt)
    {
        Status = DocumentStatus.Ready;
        ChunkCount = chunkCount;
        ProcessedAt = processedAt;
    }

    public void MarkFailed(string error)
    {
        Status = DocumentStatus.Failed;
        Error = error.Length > 2000 ? error[..2000] : error;
    }
}
