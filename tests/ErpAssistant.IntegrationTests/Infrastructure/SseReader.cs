using FluentAssertions;
using System.Net.Http.Json;
using System.Text.Json;
using ErpAssistant.Application.Ai;

namespace ErpAssistant.IntegrationTests.Infrastructure;

public sealed record SseEvent(string Type, JsonElement Data);

public static class SseReader
{
    public static async Task<List<SseEvent>> AskAsync(this HttpClient client, string message)
    {
        var response = await client.PostAsJsonAsync("/api/v1/chat", new ChatRequest(message, null));
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");

        var body = await response.Content.ReadAsStringAsync();
        var events = new List<SseEvent>();
        foreach (var block in body.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            string? type = null, data = null;
            foreach (var line in block.Split('\n'))
            {
                if (line.StartsWith("event: ", StringComparison.Ordinal)) type = line[7..];
                else if (line.StartsWith("data: ", StringComparison.Ordinal)) data = line[6..];
            }
            if (type is not null && data is not null)
                events.Add(new SseEvent(type, JsonDocument.Parse(data).RootElement.Clone()));
        }
        return events;
    }
}
