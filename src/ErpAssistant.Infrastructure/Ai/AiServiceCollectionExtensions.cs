using ErpAssistant.Application.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;

namespace ErpAssistant.Infrastructure.Ai;

public static class AiServiceCollectionExtensions
{
    public static IServiceCollection AddAi(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.TryAddSingleton<IChatClientFactory, ProviderChatClientFactory>();

        services.AddSingleton(sp =>
        {
            var factory = sp.GetRequiredService<IChatClientFactory>();
            var o = sp.GetRequiredService<IOptions<AiOptions>>().Value;
            return new AiModelInfo(factory.Provider, factory.Model, o.EmbeddingProvider switch
            {
                "OpenAI" => o.OpenAI.EmbeddingModel,
                "Gemini" => o.Gemini.EmbeddingModel,
                _ => o.Ollama.EmbeddingModel,
            });
        });

        // Ham istemci → seçenek ayarları → araç döngüsü → günlük. Araçlar her istekte ChatOptions.Tools ile
        // gelir (kullanıcının yetkisine bağlı kapsamlı servislere bağlıdırlar), bu yüzden istemci singleton olabilir.
        services.AddSingleton<IChatClient>(sp =>
        {
            var factory = sp.GetRequiredService<IChatClientFactory>();
            var o = sp.GetRequiredService<IOptions<AiOptions>>().Value;

            var builder = new ChatClientBuilder(factory.Create());
            if (factory.Provider == "Ollama")
            {
                builder.ConfigureOptions(opts =>
                {
                    opts.AdditionalProperties ??= [];
                    opts.AdditionalProperties["num_ctx"] = o.Ollama.ContextLength;
                });
            }
            return builder
                .UseFunctionInvocation(sp.GetRequiredService<ILoggerFactory>(), c =>
                {
                    c.MaximumIterationsPerRequest = o.MaxToolIterations;
                    c.AllowConcurrentInvocation = false;
                    c.IncludeDetailedErrors = false;
                })
                .UseLogging(sp.GetRequiredService<ILoggerFactory>())
                .Build(sp);
        });

        return services;
    }
}
