using ErpAssistant.Application.Ai;
using ErpAssistant.Application.Documents;
using ErpAssistant.Infrastructure.Ai;
using ErpAssistant.Infrastructure.Messaging;
using ErpAssistant.Infrastructure.Vector;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Qdrant.Client;

namespace ErpAssistant.Infrastructure.Documents;

public static class DocumentPipelineExtensions
{
    /// <summary>Doküman yükleme, kuyruğa atma ve vektör araması (Api, Worker, Mcp).</summary>
    public static IServiceCollection AddDocumentPipeline(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.Configure<QdrantOptions>(configuration.GetSection(QdrantOptions.SectionName));

        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<IDocumentQueue, RabbitMqDocumentQueue>();

        services.TryAddSingleton<IEmbeddingGeneratorFactory, ProviderEmbeddingGeneratorFactory>();
        services.AddSingleton(sp => sp.GetRequiredService<IEmbeddingGeneratorFactory>().Create());
        services.AddSingleton(sp =>
        {
            var o = sp.GetRequiredService<IOptions<QdrantOptions>>().Value;
            return new QdrantClient(o.Host, o.Port);
        });
        services.AddSingleton<QdrantDocumentIndex>();
        services.AddSingleton<IDocumentSearch>(sp => sp.GetRequiredService<QdrantDocumentIndex>());
        services.AddSingleton<IVectorIndex>(sp => sp.GetRequiredService<QdrantDocumentIndex>());

        services.AddScoped<DocumentService>();
        services.AddScoped<DocumentTools>();
        return services;
    }

    /// <summary>Kuyruk tüketicisi ve işleme hattı (Worker; testlerde API sürecine de eklenir).</summary>
    public static IServiceCollection AddDocumentProcessing(this IServiceCollection services)
    {
        services.AddScoped<DocumentProcessor>();
        services.AddHostedService<DocumentConsumer>();
        return services;
    }
}
