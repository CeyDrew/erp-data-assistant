namespace ErpAssistant.Application.Documents;

public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string fileName, CancellationToken ct);
    Task<Stream> OpenAsync(string path, CancellationToken ct);
    Task DeleteAsync(string path, CancellationToken ct);
}

/// <summary>Doküman işleme kuyruğu (RabbitMQ).</summary>
public interface IDocumentQueue
{
    Task PublishAsync(int documentId, CancellationToken ct);
}

/// <summary>Vektör indeksinden doküman silme (Qdrant).</summary>
public interface IVectorIndex
{
    Task DeleteDocumentAsync(int documentId, CancellationToken ct);
}
