using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ErpAssistant.Application.Ai;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ErpAssistant.Api.Controllers;

public sealed class ChatController(AssistantService assistant, AiModelInfo model) : ApiControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Soruyu asistana iletir; cevap Server-Sent Events olarak akar. Olay türleri: tool_call, tool_result,
    /// delta, sources, done, error.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting("chat")]
    [Produces("text/event-stream")]
    public IResult Ask(ChatRequest request, CancellationToken ct) =>
        TypedResults.ServerSentEvents(Stream(request, ct));

    [HttpGet("model")]
    public AiModelInfo Model() => model;

    private async IAsyncEnumerable<SseItem<string>> Stream(ChatRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var e in assistant.AskAsync(request, "web", ct))
            yield return new SseItem<string>(JsonSerializer.Serialize(e, e.GetType(), Json), e.Type);
    }
}
