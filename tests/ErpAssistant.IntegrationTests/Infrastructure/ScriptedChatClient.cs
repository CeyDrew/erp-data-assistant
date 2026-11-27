using System.Runtime.CompilerServices;
using System.Text.Json;
using ErpAssistant.Infrastructure.Ai;
using Microsoft.Extensions.AI;

namespace ErpAssistant.IntegrationTests.Infrastructure;

/// <summary>
/// Deterministik sahte model. Kullanıcı mesajı <c>TOOL:ad:{json}</c> biçimindeyse önce o aracı çağırır,
/// araç sonucu gelince sonucu özetleyen sabit bir metin döner. Gerçek <see cref="FunctionInvokingChatClient"/>
/// bu istemcinin önünde çalıştığından araç döngüsü, yetki filtreleri ve günlük gerçek koddan geçer.
/// </summary>
public sealed class ScriptedChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        GetStreamingResponseAsync(messages, options, cancellationToken).ToChatResponseAsync(cancellationToken);

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        var list = messages.ToList();
        var last = list[^1];

        var toolResult = last.Contents.OfType<FunctionResultContent>().FirstOrDefault();
        if (toolResult is not null)
        {
            var json = JsonSerializer.Serialize(toolResult.Result, AIJsonUtilities.DefaultOptions);
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Araç sonucu: ");
            yield return new ChatResponseUpdate(ChatRole.Assistant, json.Length > 200 ? json[..200] : json);
            yield return new ChatResponseUpdate { Contents = [new UsageContent(new UsageDetails { InputTokenCount = 50, OutputTokenCount = 10 })] };
            yield break;
        }

        var text = last.Text;
        if (text.StartsWith("TOOL:", StringComparison.Ordinal))
        {
            var parts = text.Split(':', 3);
            var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(parts[2]);
            yield return new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                Contents = [new FunctionCallContent("call-1", parts[1], args)],
            };
            yield break;
        }

        yield return new ChatResponseUpdate(ChatRole.Assistant, "Merhaba! Size nasıl yardımcı olabilirim?");
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}

public sealed class ScriptedChatClientFactory : IChatClientFactory
{
    public string Provider => "Scripted";
    public string Model => "test-model";
    public IChatClient Create() => new ScriptedChatClient();
}
