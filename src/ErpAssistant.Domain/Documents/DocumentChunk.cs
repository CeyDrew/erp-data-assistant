using ErpAssistant.Domain.Common;

namespace ErpAssistant.Domain.Documents;

/// <summary>Parçanın metadata'sı; metnin kendisi ve vektörü Qdrant'ta durur.</summary>
public sealed class DocumentChunk : Entity
{
    public int DocumentId { get; set; }
    public Document Document { get; set; } = null!;
    public int ChunkIndex { get; set; }
    public int PageNumber { get; set; }
    public int TokenCount { get; set; }
    public Guid VectorId { get; set; }
}
