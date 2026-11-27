using System.Diagnostics;
using Microsoft.Extensions.AI;

namespace ErpAssistant.Application.Ai;

public sealed record ToolCallRecord(
    string CallId, string Name, string Arguments, int RowCount, long DurationMs, string? Error);

public sealed record DocumentSource(int DocumentId, string FileName, int PageNumber, double Score);

/// <summary>
/// Bir sohbet turunda çağrılan araçların kaydı (istek kapsamlı). Sorgu günlüğüne, SSE olaylarına
/// ve MCP yanıtlarına buradan yazılır.
/// </summary>
public sealed class ToolCallRecorder
{
    private readonly List<ToolCallRecord> _calls = [];
    private readonly List<DocumentSource> _sources = [];

    public IReadOnlyList<ToolCallRecord> Calls => _calls;
    public IReadOnlyList<DocumentSource> Sources => _sources;

    public ToolCallRecord? Find(string callId) => _calls.LastOrDefault(c => c.CallId == callId);

    internal async Task<object> RunAsync(string name, object arguments, Func<Task<(object Result, int Rows)>> body)
    {
        var callId = FunctionInvokingChatClient.CurrentContext?.CallContent.CallId ?? Guid.NewGuid().ToString("N");
        var args = System.Text.Json.JsonSerializer.Serialize(arguments, AIJsonUtilities.DefaultOptions);
        var sw = Stopwatch.StartNew();
        try
        {
            var (result, rows) = await body();
            _calls.Add(new ToolCallRecord(callId, name, args, rows, sw.ElapsedMilliseconds, null));
            return result;
        }
        catch (Exception ex) when (ex is ToolArgumentException or Common.ForbiddenException or Common.NotFoundException)
        {
            // Model'e açıklayıcı bir hata döner; model kullanıcıya nedenini söyler, uydurmaz.
            _calls.Add(new ToolCallRecord(callId, name, args, 0, sw.ElapsedMilliseconds, ex.Message));
            return new { hata = ex.Message };
        }
    }

    internal void AddSources(IEnumerable<DocumentSource> sources) => _sources.AddRange(sources);
}

/// <summary>Araç parametresi sunucu tarafı doğrulamadan geçemedi.</summary>
public sealed class ToolArgumentException(string message) : Exception(message);
