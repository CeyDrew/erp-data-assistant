using System.Text.Json;
using ErpAssistant.Infrastructure.Documents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ErpAssistant.Infrastructure.Messaging;

/// <summary>
/// <c>documents.process</c> kuyruğunu tüketir. Hata olursa mesaj artan deneme sayısıyla gecikmeli yeniden
/// deneme kuyruğuna gider; <see cref="RabbitMqOptions.MaxAttempts"/> aşılınca dead-letter kuyruğuna taşınır ve
/// doküman "Hata" durumuna alınır.
/// </summary>
public sealed class DocumentConsumer(
    RabbitMqConnection connection,
    IServiceScopeFactory scopes,
    IOptions<RabbitMqOptions> options,
    ILogger<DocumentConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var conn = await connection.GetAsync(stoppingToken);
        var channel = await conn.CreateChannelAsync(cancellationToken: stoppingToken);
        await channel.BasicQosAsync(0, prefetchCount: 1, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, ea) => HandleAsync(channel, ea, stoppingToken);
        await channel.BasicConsumeAsync(RabbitMqOptions.ProcessQueue, autoAck: false, consumer, stoppingToken);
        logger.LogInformation("Doküman kuyruğu dinleniyor: {Queue}", RabbitMqOptions.ProcessQueue);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // kapanış
        }
        await channel.CloseAsync();
        await channel.DisposeAsync();
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        var attempt = ReadAttempt(ea.BasicProperties);
        DocumentUploaded? message = null;
        try
        {
            message = JsonSerializer.Deserialize<DocumentUploaded>(ea.Body.Span)
                      ?? throw new InvalidOperationException("Boş mesaj");

            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DocumentProcessor>().ProcessAsync(message.DocumentId, ct);
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            var max = options.Value.MaxAttempts;
            logger.LogWarning(ex, "Doküman {Id} işlenemedi (deneme {Attempt}/{Max})", message?.DocumentId, attempt, max);

            var props = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = ea.BasicProperties.MessageId,
                Headers = new Dictionary<string, object?>
                {
                    [RabbitMqOptions.AttemptHeader] = attempt + 1,
                    ["x-last-error"] = Truncate(ex.Message, 500),
                },
            };

            if (attempt < max && message is not null)
            {
                await channel.BasicPublishAsync(RabbitMqOptions.Exchange, RabbitMqOptions.RetryKey, false, props, ea.Body, ct);
            }
            else
            {
                await channel.BasicPublishAsync(RabbitMqOptions.Exchange, RabbitMqOptions.DeadKey, false, props, ea.Body, ct);
                if (message is not null)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<DocumentProcessor>()
                        .MarkFailedAsync(message.DocumentId, $"{attempt} denemede işlenemedi: {ex.Message}", ct);
                }
            }
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
        }
    }

    private static int ReadAttempt(IReadOnlyBasicProperties props) =>
        props.Headers?.TryGetValue(RabbitMqOptions.AttemptHeader, out var v) == true
            ? v switch { int i => i, long l => (int)l, byte b => b, _ => 1 }
            : 1;

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
