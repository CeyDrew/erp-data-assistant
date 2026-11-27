using ErpAssistant.Application.Abstractions;
using ErpAssistant.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace ErpAssistant.Application.Ai;

public sealed class AiLogService(IErpDbContext db)
{
    public Task<PagedResult<AiLogItem>> ListAsync(AiLogQuery q, CancellationToken ct)
    {
        var query = db.AiQueryLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Channel))
            query = query.Where(l => l.Channel == q.Channel);

        return query.OrderByDescending(l => l.Id)
            .Select(l => new AiLogItem(l.Id, l.CreatedAt, l.UserName, l.Channel, l.Question, l.Answer, l.ToolCallsJson,
                l.TotalRowCount, l.DurationMs, l.InputTokens, l.OutputTokens, l.Model, l.Error))
            .ToPagedAsync(q, ct);
    }
}
