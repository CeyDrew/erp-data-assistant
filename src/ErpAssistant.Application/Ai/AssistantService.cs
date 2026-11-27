using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ErpAssistant.Application.Abstractions;
using ErpAssistant.Domain.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ErpAssistant.Application.Ai;

/// <summary>
/// Soru → (araç çağrıları) → cevap akışı. Araç döngüsünü <see cref="FunctionInvokingChatClient"/> yürütür;
/// bu sınıf akışı olaylara çevirir ve her soruyu günlüğe yazar.
/// </summary>
public sealed partial class AssistantService(
    IChatClient chat,
    ErpTools tools,
    ToolCallRecorder recorder,
    IErpDbContext db,
    ICurrentUser user,
    TimeProvider clock,
    AiModelInfo model,
    ILogger<AssistantService> logger,
    DocumentTools? documents = null)
{
    public async IAsyncEnumerable<AssistantEvent> AskAsync(
        ChatRequest request, string channel, [EnumeratorCancellation] CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var today = clock.GetUtcNow().UtcDateTime.Date;

        var messages = new List<ChatMessage> { new(ChatRole.System, AssistantPrompt.Build(user, today)) };
        foreach (var turn in request.History ?? [])
            messages.Add(new ChatMessage(turn.Role == "assistant" ? ChatRole.Assistant : ChatRole.User, turn.Content));
        // Küçük modeller doküman sorularını bazen aramadan "bulamadım" diye cevaplıyor. Anahtar kelimeye dayalı
        // basit bir yönlendirme notu bunu gideriyor (model yine de aracı kendi seçer). Not, ayrı bir sistem mesajı
        // yerine kullanıcı mesajına eklenir: sondaki sistem mesajı bazı modellerin sohbet şablonunu bozuyor.
        var question = documents is not null && DocumentHint.IsMatch(request.Message)
            ? request.Message + "\n\n(Not: bu soru şirket dokümanlarıyla ilgili; önce dokumanda_ara aracını kullan.)"
            : request.Message;
        messages.Add(new ChatMessage(ChatRole.User, question));

        var options = new ChatOptions
        {
            Tools = [.. ErpToolCatalog.Create(tools, documents)],
            ToolMode = ChatToolMode.Auto,
            Temperature = 0.1f,
        };

        var answer = new StringBuilder();
        long? inputTokens = null, outputTokens = null;
        string? error = null;
        var announced = new HashSet<string>();

        await using var stream = chat.GetStreamingResponseAsync(messages, options, ct).GetAsyncEnumerator(ct);
        while (true)
        {
            AssistantEvent? failure = null;
            bool hasNext;
            try
            {
                hasNext = await stream.MoveNextAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "LLM çağrısı başarısız");
                error = ex.Message;
                failure = new ErrorEvent("Model sağlayıcısına ulaşılamadı veya istek başarısız oldu. Ayrıntı günlükte.");
                hasNext = false;
            }

            if (failure is not null)
            {
                yield return failure;
                break;
            }
            if (!hasNext) break;

            foreach (var content in stream.Current.Contents)
            {
                switch (content)
                {
                    case FunctionCallContent call when announced.Add(call.CallId):
                        yield return new ToolCallEvent(call.CallId, call.Name,
                            JsonSerializer.Serialize(call.Arguments, AIJsonUtilities.DefaultOptions));
                        break;
                    case FunctionResultContent result:
                        var record = recorder.Find(result.CallId);
                        yield return new ToolResultEvent(result.CallId, record?.Name ?? "?", record?.RowCount ?? 0,
                            record?.DurationMs ?? 0, record?.Error ?? result.Exception?.Message);
                        break;
                    case TextContent { Text.Length: > 0 } text:
                        answer.Append(text.Text);
                        yield return new TextDeltaEvent(text.Text);
                        break;
                    case UsageContent usage:
                        inputTokens = (inputTokens ?? 0) + usage.Details.InputTokenCount;
                        outputTokens = (outputTokens ?? 0) + usage.Details.OutputTokenCount;
                        break;
                }
            }
        }

        if (recorder.Sources.Count > 0)
            yield return new SourcesEvent(recorder.Sources.DistinctBy(s => (s.DocumentId, s.PageNumber)).ToList());

        var log = new AiQueryLog
        {
            UserId = user.UserId ?? "?",
            UserName = user.UserName ?? "?",
            Channel = channel,
            Question = request.Message,
            Answer = answer.ToString(),
            ToolCallsJson = JsonSerializer.Serialize(recorder.Calls, AIJsonUtilities.DefaultOptions),
            TotalRowCount = recorder.Calls.Sum(c => c.RowCount),
            DurationMs = sw.ElapsedMilliseconds,
            InputTokens = (int?)inputTokens,
            OutputTokens = (int?)outputTokens,
            Model = $"{model.Provider}/{model.ChatModel}",
            Error = error,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        db.AiQueryLogs.Add(log);
        await db.SaveChangesAsync(CancellationToken.None);

        yield return new DoneEvent(log.Id, log.DurationMs, log.InputTokens, log.OutputTokens, log.Model!);
    }

    [System.Text.RegularExpressions.GeneratedRegex(
        @"şartname|prosedür|politika|yönetmelik|sözleşme|talimat|doküman|belge|kalite notu|garanti|iade|katalog",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex DocumentHint { get; }
}
