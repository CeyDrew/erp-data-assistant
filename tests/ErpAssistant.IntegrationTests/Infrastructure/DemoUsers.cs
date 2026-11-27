using System.Net.Http.Headers;
using System.Net.Http.Json;
using ErpAssistant.Application.Auth;

namespace ErpAssistant.IntegrationTests.Infrastructure;

public static class DemoUsers
{
    public const string Admin = "yonetici@erp.local";
    public const string SalesRep = "temsilci@erp.local";
    public const string OtherSalesRep = "temsilci2@erp.local";
    public const string Warehouse = "depo@erp.local";
    public const string Driver = "surucu@erp.local";
    public const string OtherDriver = "surucu2@erp.local";
    public const string Password = "Demo123!";

    public static async Task<AuthResponse> LoginAsync(this HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(HttpExtensions.Json))!;
    }

    public static async Task<HttpClient> CreateClientAsAsync(this ErpApiFactory factory, string email)
    {
        var client = factory.CreateClient();
        var auth = await client.LoginAsync(email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
