using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ErpAssistant.Application.Ai;
using ErpAssistant.Application.Common;
using ErpAssistant.Application.Customers;
using ErpAssistant.Infrastructure.Identity;
using ErpAssistant.IntegrationTests.Infrastructure;
using FluentAssertions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace ErpAssistant.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class McpServerTests(ErpApiFactory factory)
{
    private async Task<string> CreateTokenAsync(HttpClient client, string name = "test")
    {
        var response = await client.PostAsJsonAsync("/api/v1/tokens", new { name, days = 7 });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedToken>(HttpExtensions.Json))!.Token;
    }

    private async Task<McpClient> ConnectAsync(string token)
    {
        var http = factory.Mcp.CreateClient();
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(http.BaseAddress!, "/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        }, http, ownsHttpClient: true);
        return await McpClient.CreateAsync(transport);
    }

    private static string Text(CallToolResult result) =>
        string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    [Fact]
    public async Task Lists_the_same_read_only_tools_as_the_assistant()
    {
        await using var client = await ConnectAsync(await CreateTokenAsync(factory.Admin));

        var tools = await client.ListToolsAsync();

        tools.Select(t => t.Name).Should().BeEquivalentTo(ErpToolCatalog.Definitions.Select(d => d.Name));
        tools.Should().OnlyContain(t => t.ProtocolTool.Annotations!.ReadOnlyHint == true);
        tools.Single(t => t.Name == "satis_ozeti").JsonSchema.GetRawText().Should().Contain("baslangic").And.Contain("Kategori");
    }

    [Fact]
    public async Task Report_tool_runs_and_is_logged_on_the_mcp_channel()
    {
        await using var client = await ConnectAsync(await CreateTokenAsync(factory.Admin));

        var result = await client.CallToolAsync("en_cok_satanlar", new Dictionary<string, object?>
        {
            ["n"] = 3, ["baslangic"] = "2024-06-01", ["bitis"] = DateTime.UtcNow.ToString("yyyy-MM-dd"), ["olcut"] = "miktar",
        });

        result.IsError.Should().NotBe(true);
        Text(result).Should().Contain("urunler").And.MatchRegex(@"""sira"":\s*3");

        var logs = await factory.Admin.GetJsonAsync<PagedResult<AiLogItem>>("/api/v1/ailogs?channel=mcp&pageSize=50");
        logs.Items.Should().Contain(l => l.Question.StartsWith("en_cok_satanlar") && l.TotalRowCount == 3);
    }

    [Fact]
    public async Task Token_carries_the_owners_row_security()
    {
        var rep = await factory.CreateClientAsAsync(DemoUsers.SalesRep);
        var me = await rep.GetJsonAsync<ErpAssistant.Application.Auth.UserInfo>("/api/v1/auth/me");
        var all = await factory.Admin.GetJsonAsync<PagedResult<CustomerListItem>>("/api/v1/customers?pageSize=100");
        var foreign = all.Items.First(c => c.SalesRepresentativeId != me.SalesRepresentativeId);

        await using var client = await ConnectAsync(await CreateTokenAsync(rep));
        var result = await client.CallToolAsync("musteri_siparisleri",
            new Dictionary<string, object?> { ["musteri"] = foreign.Code, ["durum"] = "Tum" });

        Text(result).Should().Contain("görme yetkiniz yok");
    }

    [Fact]
    public async Task Requests_without_valid_token_are_rejected()
    {
        var http = factory.Mcp.CreateClient();
        var body = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""", System.Text.Encoding.UTF8, "application/json");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");

        (await http.PostAsync("/mcp", body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "erp_pat_uydurma");
        (await http.PostAsync("/mcp", body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Revoked_token_stops_working()
    {
        var token = await CreateTokenAsync(factory.Admin, "iptal-edilecek");
        var list = await factory.Admin.GetJsonAsync<List<TokenItem>>("/api/v1/tokens");
        var id = list.First(t => t.Name == "iptal-edilecek").Id;

        (await factory.Admin.DeleteAsync($"/api/v1/tokens/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var act = () => ConnectAsync(token);
        await act.Should().ThrowAsync<Exception>();
    }
}
