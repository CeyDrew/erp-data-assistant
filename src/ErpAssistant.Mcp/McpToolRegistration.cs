using System.Text.Json;
using ErpAssistant.Application.Abstractions;
using ErpAssistant.Application.Ai;
using ErpAssistant.Domain.Ai;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ErpAssistant.Mcp;

/// <summary>
/// Sohbet asistanının kullandığı araçları MCP aracı olarak yayınlar. Ad, açıklama ve JSON Schema aynı
/// <see cref="ErpToolCatalog.Definitions"/> listesinden gelir; her çağrıda hedef nesne isteğin kapsamından
/// (dolayısıyla token sahibinin yetkisiyle) oluşturulur.
/// </summary>
public static class McpToolRegistration
{
    public static IEnumerable<McpServerTool> CreateTools() =>
        ErpToolCatalog.Definitions.Select(d => McpServerTool.Create(
            d.Method,
            ctx => ctx.Services!.GetRequiredService(d.Method.DeclaringType!),
            new McpServerToolCreateOptions
            {
                Name = d.Name,
                ReadOnly = true,
                Destructive = false,
                Idempotent = true,
                OpenWorld = false,
                SerializerOptions = ErpToolCatalog.SerializerOptions,
            }));

    /// <summary>Her araç çağrısını AI sorgu günlüğüne "mcp" kanalıyla yazar.</summary>
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> LogCalls(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next) => async (context, ct) =>
    {
        var started = DateTime.UtcNow;
        var result = await next(context, ct);

        var services = context.Services!;
        var recorder = services.GetRequiredService<ToolCallRecorder>();
        var user = services.GetRequiredService<ICurrentUser>();
        var db = services.GetRequiredService<IErpDbContext>();
        var call = recorder.Calls.LastOrDefault();

        db.AiQueryLogs.Add(new AiQueryLog
        {
            UserId = user.UserId ?? "?",
            UserName = user.UserName ?? "?",
            Channel = "mcp",
            Question = $"{context.Params?.Name}({JsonSerializer.Serialize(context.Params?.Arguments, JsonSerializerOptions.Web)})",
            ToolCallsJson = JsonSerializer.Serialize(recorder.Calls, JsonSerializerOptions.Web),
            TotalRowCount = recorder.Calls.Sum(c => c.RowCount),
            DurationMs = (long)(DateTime.UtcNow - started).TotalMilliseconds,
            Model = "mcp-client",
            Error = result.IsError == true ? call?.Error ?? "Araç hatası" : call?.Error,
            CreatedAt = started,
        });
        await db.SaveChangesAsync(ct);
        return result;
    };
}
