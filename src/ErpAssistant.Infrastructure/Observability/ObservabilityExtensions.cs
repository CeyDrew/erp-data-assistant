using ErpAssistant.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ErpAssistant.Infrastructure.Observability;

public static class ObservabilityExtensions
{
    /// <summary>Microsoft.Extensions.AI'nin OpenTelemetry kaynak adı (GenAI semantik kuralları).</summary>
    public const string AiSourceName = "ErpAssistant.AI";

    /// <summary>
    /// İzler: ASP.NET Core, HttpClient, EF Core, LLM çağrıları (GenAI semantik kuralları), araçlar, doküman işleme.
    /// Metrikler: istekler, çalışma zamanı, token kullanımı, araç ve doküman sayaçları.
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> tanımlıysa OTLP ile dışa aktarılır (Compose'da Aspire paneli).
    /// </summary>
    public static IHostApplicationBuilder AddObservability(this IHostApplicationBuilder builder, string serviceName)
    {
        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName, serviceVersion: "1.0.0"))
            .WithTracing(t => t
                .AddSource(ErpTelemetry.Name, AiSourceName)
                .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation())
            .WithMetrics(m => m
                .AddMeter(ErpTelemetry.Name, AiSourceName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation());

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            otel.UseOtlpExporter();

        return builder;
    }
}
