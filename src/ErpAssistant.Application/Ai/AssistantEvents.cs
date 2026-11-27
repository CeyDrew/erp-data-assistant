namespace ErpAssistant.Application.Ai;

public sealed record ChatTurn(string Role, string Content);

public sealed record ChatRequest(string Message, IReadOnlyList<ChatTurn>? History);

public abstract record AssistantEvent(string Type);

public sealed record ToolCallEvent(string CallId, string Name, string Arguments) : AssistantEvent("tool_call");

public sealed record ToolResultEvent(string CallId, string Name, int RowCount, long DurationMs, string? Error)
    : AssistantEvent("tool_result");

public sealed record TextDeltaEvent(string Text) : AssistantEvent("delta");

public sealed record SourcesEvent(IReadOnlyList<DocumentSource> Sources) : AssistantEvent("sources");

public sealed record DoneEvent(int LogId, long DurationMs, int? InputTokens, int? OutputTokens, string Model)
    : AssistantEvent("done");

public sealed record ErrorEvent(string Message) : AssistantEvent("error");
