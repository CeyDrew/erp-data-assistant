namespace ErpAssistant.Infrastructure.Vector;

public sealed class QdrantOptions
{
    public const string SectionName = "Qdrant";

    public string Host { get; set; } = "localhost";

    /// <summary>gRPC portu.</summary>
    public int Port { get; set; } = 6334;

    /// <summary>Koleksiyon adı ön eki; embedding modeli eklenir (model değişince eski vektörlerle karışmaz).</summary>
    public string CollectionPrefix { get; set; } = "documents";

    /// <summary>Kosinüs benzerlik eşiği; altındaki parçalar "ilgisiz" sayılır. Embedding modeline göre ayarlanır.</summary>
    public float MinScore { get; set; } = 0.45f;
}
