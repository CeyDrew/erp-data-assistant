namespace ErpAssistant.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Uri { get; set; } = "amqp://guest:guest@localhost:5672";
    public int MaxAttempts { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 5_000;

    // Topoloji: documents (direct) → documents.process; hata → documents.retry (TTL) → tekrar documents;
    // deneme sınırı aşılınca → documents.dead (dead-letter kuyruğu).
    public const string Exchange = "documents";
    public const string ProcessQueue = "documents.process";
    public const string RetryQueue = "documents.retry";
    public const string DeadQueue = "documents.dead";
    public const string ProcessKey = "process";
    public const string RetryKey = "retry";
    public const string DeadKey = "dead";
    public const string AttemptHeader = "x-attempt";
}
