using System.Text.Json;
using ErpAssistant.Infrastructure.Ai;
using ErpAssistant.Infrastructure.Messaging;
using ErpAssistant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using RabbitMQ.Client;

namespace ErpAssistant.Infrastructure.Observability;

internal sealed class SqlServerHealthCheck(ErpDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
        await db.Database.CanConnectAsync(ct) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Bağlanılamadı");
}

internal sealed class RabbitMqHealthCheck(IOptions<RabbitMqOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var factory = new ConnectionFactory { Uri = new Uri(options.Value.Uri), RequestedConnectionTimeout = TimeSpan.FromSeconds(3) };
            await using var connection = await factory.CreateConnectionAsync(ct);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
            var dead = await channel.QueueDeclarePassiveAsync(RabbitMqOptions.DeadQueue, ct);
            return HealthCheckResult.Healthy(data: new Dictionary<string, object> { ["deadLetters"] = dead.MessageCount });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}

internal sealed class QdrantHealthCheck(QdrantClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var reply = await client.HealthAsync(ct);
            return HealthCheckResult.Healthy(reply.Version);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}

internal sealed class OllamaHealthCheck(IOptions<AiOptions> options, IHttpClientFactory http) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var o = options.Value;
        if (o.Provider != "Ollama" && o.EmbeddingProvider != "Ollama")
            return HealthCheckResult.Healthy("Ollama kullanılmıyor");
        try
        {
            using var client = http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3);
            var tags = await client.GetFromJsonAsyncSafe(new Uri(new Uri(o.Ollama.Endpoint), "/api/tags"), ct);
            var required = new[] { o.Ollama.ChatModel, o.Ollama.EmbeddingModel };
            var missing = required.Where(m => !tags.Any(t => t == m || t == $"{m}:latest")).ToList();
            return missing.Count == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded($"Model indirilmemiş: {string.Join(", ", missing)} (ollama pull)");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}

internal static class OllamaTags
{
    public static async Task<IReadOnlyList<string>> GetFromJsonAsyncSafe(this HttpClient client, Uri uri, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(await client.GetStringAsync(uri, ct));
        return doc.RootElement.GetProperty("models").EnumerateArray().Select(m => m.GetProperty("name").GetString()!).ToList();
    }
}

public static class HealthCheckExtensions
{
    public const string Ready = "ready";

    public static IHealthChecksBuilder AddErpHealthChecks(this IServiceCollection services, bool documents, bool ai)
    {
        services.AddHttpClient();
        var builder = services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>("sqlserver", tags: [Ready]);
        if (documents)
        {
            builder.AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: [Ready]);
            builder.AddCheck<QdrantHealthCheck>("qdrant", tags: [Ready]);
        }
        if (ai)
            builder.AddCheck<OllamaHealthCheck>("ollama", HealthStatus.Degraded, tags: [Ready]);
        return builder;
    }

    /// <summary><c>/health</c>: süreç ayakta mı (liveness). <c>/health/ready</c>: bağımlılıklar (readiness), JSON ayrıntılı.</summary>
    public static IEndpointRouteBuilder MapErpHealthChecks(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = c => c.Tags.Contains(Ready),
            ResponseWriter = WriteJson,
        });
        return app;
    }

    private static Task WriteJson(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            durationMs = (int)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.ToDictionary(e => e.Key, e => new
            {
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                data = e.Value.Data.Count == 0 ? null : e.Value.Data,
            }),
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
