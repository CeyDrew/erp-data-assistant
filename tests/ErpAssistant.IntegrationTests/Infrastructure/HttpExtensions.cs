using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErpAssistant.IntegrationTests.Infrastructure;

public static class HttpExtensions
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<T> GetJsonAsync<T>(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"{url} → {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }
}
