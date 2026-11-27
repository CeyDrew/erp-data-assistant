using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ErpAssistant.Infrastructure.Messaging;

/// <summary>Uygulama başına tek bağlantı; topoloji ilk bağlantıda kurulur.</summary>
public sealed class RabbitMqConnection(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConnection> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public async Task<IConnection> GetAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true }) return _connection;

        await _gate.WaitAsync(ct);
        try
        {
            if (_connection is { IsOpen: true }) return _connection;

            var factory = new ConnectionFactory { Uri = new Uri(options.Value.Uri), AutomaticRecoveryEnabled = true, ClientProvidedName = "erp-assistant" };
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    _connection = await factory.CreateConnectionAsync(ct);
                    break;
                }
                catch (Exception ex) when (attempt < 10 && ex is not OperationCanceledException)
                {
                    logger.LogWarning("RabbitMQ'ya bağlanılamadı ({Attempt}/10): {Message}", attempt, ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(attempt * 2, 10)), ct);
                }
            }

            await using var channel = await _connection.CreateChannelAsync(cancellationToken: ct);
            await DeclareTopologyAsync(channel, options.Value, ct);
            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public static async Task DeclareTopologyAsync(IChannel channel, RabbitMqOptions o, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(RabbitMqOptions.Exchange, ExchangeType.Direct, durable: true, cancellationToken: ct);

        await channel.QueueDeclareAsync(RabbitMqOptions.ProcessQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = RabbitMqOptions.Exchange,
                ["x-dead-letter-routing-key"] = RabbitMqOptions.DeadKey,
            }, cancellationToken: ct);

        // Gecikmeli yeniden deneme: mesaj TTL kadar bekler, süre dolunca ana kuyruğa geri döner.
        await channel.QueueDeclareAsync(RabbitMqOptions.RetryQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = o.RetryDelayMs,
                ["x-dead-letter-exchange"] = RabbitMqOptions.Exchange,
                ["x-dead-letter-routing-key"] = RabbitMqOptions.ProcessKey,
            }, cancellationToken: ct);

        await channel.QueueDeclareAsync(RabbitMqOptions.DeadQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);

        await channel.QueueBindAsync(RabbitMqOptions.ProcessQueue, RabbitMqOptions.Exchange, RabbitMqOptions.ProcessKey, cancellationToken: ct);
        await channel.QueueBindAsync(RabbitMqOptions.RetryQueue, RabbitMqOptions.Exchange, RabbitMqOptions.RetryKey, cancellationToken: ct);
        await channel.QueueBindAsync(RabbitMqOptions.DeadQueue, RabbitMqOptions.Exchange, RabbitMqOptions.DeadKey, cancellationToken: ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null) await _connection.DisposeAsync();
        _gate.Dispose();
    }
}
