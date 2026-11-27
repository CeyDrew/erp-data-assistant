using System.Text.Json;
using ErpAssistant.Application.Documents;
using RabbitMQ.Client;

namespace ErpAssistant.Infrastructure.Messaging;

internal sealed class RabbitMqDocumentQueue(RabbitMqConnection connection) : IDocumentQueue
{
    public async Task PublishAsync(int documentId, CancellationToken ct)
    {
        var conn = await connection.GetAsync(ct);
        await using var channel = await conn.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true), ct);

        var body = JsonSerializer.SerializeToUtf8Bytes(new DocumentUploaded(documentId));
        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString("N"),
            Headers = new Dictionary<string, object?> { [RabbitMqOptions.AttemptHeader] = 1 },
        };
        await channel.BasicPublishAsync(RabbitMqOptions.Exchange, RabbitMqOptions.ProcessKey, mandatory: true, props, body, ct);
    }
}
