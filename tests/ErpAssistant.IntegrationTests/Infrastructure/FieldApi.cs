using System.Net.Http.Json;
using System.Text.Json;
using ErpAssistant.Application.Field;
using ErpAssistant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ErpAssistant.IntegrationTests.Infrastructure;

/// <summary>Saha (mobil) API'si için test yardımcıları.</summary>
public static class FieldApi
{
    public static Task<SyncChangesResponse> SyncAsync(this HttpClient client, string? since = null, int? limit = null)
    {
        var query = new List<string>();
        if (since is not null) query.Add($"since={since}");
        if (limit is not null) query.Add($"limit={limit}");
        return client.GetJsonAsync<SyncChangesResponse>("/api/v1/field/sync/changes" + (query.Count > 0 ? "?" + string.Join('&', query) : ""));
    }

    /// <summary>hasMore bitene kadar sayfalayarak tam senkron; son imleç ve birleştirilmiş sonuç.</summary>
    public static async Task<(string Cursor, SyncChangesResponse Last, List<SyncChangesResponse> Pages)> FullSyncAsync(
        this HttpClient client, string? since = null, int? limit = null)
    {
        var pages = new List<SyncChangesResponse>();
        var cursor = since;
        SyncChangesResponse page;
        do
        {
            page = await client.SyncAsync(cursor, limit);
            pages.Add(page);
            cursor = page.Cursor;
        } while (page.HasMore && pages.Count < 100);
        return (page.Cursor, page, pages);
    }

    public static Task<HttpResponseMessage> PostOrderAsync(this HttpClient client, Guid? key, FieldOrderRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/field/orders") { Content = JsonContent.Create(request) };
        if (key is { } k) message.Headers.Add("Idempotency-Key", k.ToString());
        return client.SendAsync(message);
    }

    public static async Task<PushResponse> PushAsync(this HttpClient client, params object[] operations)
    {
        var response = await client.PostAsJsonAsync("/api/v1/field/sync/push", new { operations });
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"push → {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<PushResponse>(HttpExtensions.Json))!;
    }

    public static object Op(Guid id, string type, object payload) => new { id, type, payload };

    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    /// <summary>Filtresiz (sistem kullanıcısı) veritabanı erişimi: test verisini doğrudan değiştirmek için.</summary>
    public static async Task WithDbAsync(this ErpApiFactory factory, Func<ErpDbContext, Task> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<ErpDbContext>());
    }

    public static async Task<T> WithDbAsync<T>(this ErpApiFactory factory, Func<ErpDbContext, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<ErpDbContext>());
    }
}
