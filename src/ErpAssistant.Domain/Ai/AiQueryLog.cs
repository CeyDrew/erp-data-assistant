using ErpAssistant.Domain.Common;

namespace ErpAssistant.Domain.Ai;

public sealed class AiQueryLog : Entity
{
    public required string UserId { get; set; }
    public required string UserName { get; set; }

    /// <summary>"web" veya "mcp".</summary>
    public required string Channel { get; set; }

    public required string Question { get; set; }
    public string? Answer { get; set; }

    /// <summary>JSON dizisi: [{ name, arguments, rowCount, durationMs, error }]</summary>
    public string ToolCallsJson { get; set; } = "[]";

    public int TotalRowCount { get; set; }
    public long DurationMs { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public string? Model { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
}
