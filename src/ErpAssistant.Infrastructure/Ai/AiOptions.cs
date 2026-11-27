namespace ErpAssistant.Infrastructure.Ai;

/// <summary>
/// Sağlayıcı seçimi yalnızca yapılandırmayla değişir (<c>Ai:Provider</c>, <c>Ai:EmbeddingProvider</c>);
/// uygulama kodu yalnızca <c>IChatClient</c> ve <c>IEmbeddingGenerator</c> görür.
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Ollama | OpenAI | Gemini | Anthropic</summary>
    public string Provider { get; set; } = "Ollama";

    /// <summary>Ollama | OpenAI | Gemini (Anthropic'in embedding API'si yok)</summary>
    public string EmbeddingProvider { get; set; } = "Ollama";

    public int MaxToolIterations { get; set; } = 6;

    public OllamaSettings Ollama { get; set; } = new();
    public ProviderSettings OpenAI { get; set; } = new() { ChatModel = "gpt-4.1-mini", EmbeddingModel = "text-embedding-3-small" };
    public ProviderSettings Gemini { get; set; } = new()
    {
        ChatModel = "gemini-2.5-flash",
        EmbeddingModel = "gemini-embedding-001",
        Endpoint = "https://generativelanguage.googleapis.com/v1beta/openai/",
    };
    public ProviderSettings Anthropic { get; set; } = new() { ChatModel = "claude-opus-5-5" };

    public sealed class OllamaSettings
    {
        public string Endpoint { get; set; } = "http://localhost:11434";
        public string ChatModel { get; set; } = "qwen2.5:7b";
        public string EmbeddingModel { get; set; } = "bge-m3";
        public int ContextLength { get; set; } = 8192;
    }

    public sealed class ProviderSettings
    {
        public string? ApiKey { get; set; }
        public string ChatModel { get; set; } = "";
        public string? EmbeddingModel { get; set; }
        public string? Endpoint { get; set; }
    }
}
