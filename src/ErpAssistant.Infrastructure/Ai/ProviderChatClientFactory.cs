using System.ClientModel;
using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OpenAI;

namespace ErpAssistant.Infrastructure.Ai;

internal sealed class ProviderChatClientFactory(IOptions<AiOptions> options) : IChatClientFactory
{
    private AiOptions O => options.Value;

    public string Provider => O.Provider;

    public string Model => O.Provider switch
    {
        "OpenAI" => O.OpenAI.ChatModel,
        "Gemini" => O.Gemini.ChatModel,
        "Anthropic" => O.Anthropic.ChatModel,
        _ => O.Ollama.ChatModel,
    };

    public IChatClient Create() => O.Provider switch
    {
        "Ollama" => new OllamaApiClient(new Uri(O.Ollama.Endpoint), O.Ollama.ChatModel),
        "OpenAI" => new OpenAIClient(RequireKey(O.OpenAI, "OpenAI"))
            .GetChatClient(O.OpenAI.ChatModel).AsIChatClient(),
        // Gemini'nin OpenAI uyumlu uç noktası; ayrı SDK gerektirmez.
        "Gemini" => new OpenAIClient(new ApiKeyCredential(RequireKey(O.Gemini, "Gemini")),
                new OpenAIClientOptions { Endpoint = new Uri(O.Gemini.Endpoint!) })
            .GetChatClient(O.Gemini.ChatModel).AsIChatClient(),
        "Anthropic" => new AnthropicClient { ApiKey = RequireKey(O.Anthropic, "Anthropic") }
            .AsIChatClient(O.Anthropic.ChatModel),
        _ => throw new InvalidOperationException($"Bilinmeyen Ai:Provider '{O.Provider}'. Ollama, OpenAI, Gemini veya Anthropic olmalı."),
    };

    private static string RequireKey(AiOptions.ProviderSettings s, string name) =>
        string.IsNullOrWhiteSpace(s.ApiKey)
            ? throw new InvalidOperationException($"Ai:{name}:ApiKey tanımlı değil.")
            : s.ApiKey;
}
