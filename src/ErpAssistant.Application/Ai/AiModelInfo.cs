namespace ErpAssistant.Application.Ai;

/// <summary>Etkin sağlayıcı ve model; günlüğe ve arayüze yazılır.</summary>
public sealed record AiModelInfo(string Provider, string ChatModel, string? EmbeddingModel);
