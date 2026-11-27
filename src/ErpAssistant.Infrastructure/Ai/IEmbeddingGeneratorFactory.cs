using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OpenAI;

namespace ErpAssistant.Infrastructure.Ai;

public interface IEmbeddingGeneratorFactory
{
    string Model { get; }
    IEmbeddingGenerator<string, Embedding<float>> Create();
}

internal sealed class ProviderEmbeddingGeneratorFactory(IOptions<AiOptions> options) : IEmbeddingGeneratorFactory
{
    private AiOptions O => options.Value;

    public string Model => O.EmbeddingProvider switch
    {
        "OpenAI" => O.OpenAI.EmbeddingModel!,
        "Gemini" => O.Gemini.EmbeddingModel!,
        _ => O.Ollama.EmbeddingModel,
    };

    public IEmbeddingGenerator<string, Embedding<float>> Create() => O.EmbeddingProvider switch
    {
        "Ollama" => new OllamaApiClient(new Uri(O.Ollama.Endpoint), O.Ollama.EmbeddingModel),
        "OpenAI" => new OpenAIClient(O.OpenAI.ApiKey ?? throw new InvalidOperationException("Ai:OpenAI:ApiKey tanımlı değil."))
            .GetEmbeddingClient(O.OpenAI.EmbeddingModel).AsIEmbeddingGenerator(),
        "Gemini" => new OpenAIClient(new ApiKeyCredential(O.Gemini.ApiKey ?? throw new InvalidOperationException("Ai:Gemini:ApiKey tanımlı değil.")),
                new OpenAIClientOptions { Endpoint = new Uri(O.Gemini.Endpoint!) })
            .GetEmbeddingClient(O.Gemini.EmbeddingModel).AsIEmbeddingGenerator(),
        _ => throw new InvalidOperationException($"Ai:EmbeddingProvider '{O.EmbeddingProvider}' desteklenmiyor (Ollama, OpenAI, Gemini)."),
    };
}
