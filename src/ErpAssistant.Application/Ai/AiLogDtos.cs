using ErpAssistant.Application.Common;

namespace ErpAssistant.Application.Ai;

public sealed record AiLogQuery : PageQuery
{
    public string? Channel { get; init; }
}

public sealed class AiLogQueryValidator : PageQueryValidator<AiLogQuery>;

public sealed record AiLogItem(
    int Id, DateTime CreatedAt, string UserName, string Channel, string Question, string? Answer, string ToolCallsJson,
    int TotalRowCount, long DurationMs, int? InputTokens, int? OutputTokens, string? Model, string? Error);
