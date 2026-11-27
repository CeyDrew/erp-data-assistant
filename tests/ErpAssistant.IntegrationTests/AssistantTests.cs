using ErpAssistant.Application.Ai;
using ErpAssistant.Application.Common;
using ErpAssistant.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace ErpAssistant.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AssistantTests(ErpApiFactory factory)
{
    private static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");

    [Fact]
    public async Task Tool_call_streams_events_and_is_logged()
    {
        var events = await factory.Admin.AskAsync(
            $$"""TOOL:en_cok_satanlar:{"n":3,"baslangic":"2024-06-01","bitis":"{{Today}}","olcut":"Ciro"}""");

        events.Select(e => e.Type).Should().ContainInOrder("tool_call", "tool_result", "delta", "done");
        var result = events.First(e => e.Type == "tool_result").Data;
        result.GetProperty("name").GetString().Should().Be("en_cok_satanlar");
        result.GetProperty("rowCount").GetInt32().Should().Be(3);
        result.GetProperty("error").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);

        var logId = events.Single(e => e.Type == "done").Data.GetProperty("logId").GetInt32();
        var logs = await factory.Admin.GetJsonAsync<PagedResult<AiLogItem>>("/api/v1/ailogs?pageSize=50");
        var log = logs.Items.Single(l => l.Id == logId);
        log.ToolCallsJson.Should().Contain("en_cok_satanlar");
        log.TotalRowCount.Should().Be(3);
        log.InputTokens.Should().Be(50);
        log.Model.Should().Be("Scripted/test-model");
    }

    [Fact]
    public async Task Invalid_tool_arguments_are_rejected_server_side()
    {
        var events = await factory.Admin.AskAsync(
            $$"""TOOL:en_cok_satanlar:{"n":500,"baslangic":"2024-06-01","bitis":"{{Today}}"}""");

        var result = events.First(e => e.Type == "tool_result").Data;
        result.GetProperty("error").GetString().Should().Contain("n 1 ile 20");
        result.GetProperty("rowCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Malformed_date_is_rejected()
    {
        var events = await factory.Admin.AskAsync("""TOOL:satis_ozeti:{"baslangic":"geçen ay","bitis":"2026-01-01"}""");

        events.First(e => e.Type == "tool_result").Data.GetProperty("error").GetString().Should().Contain("yyyy-MM-dd");
    }

    [Fact]
    public async Task Warehouse_user_cannot_read_receivables_through_the_assistant()
    {
        var client = await factory.CreateClientAsAsync(DemoUsers.Warehouse);

        var events = await client.AskAsync("""TOOL:geciken_tahsilatlar:{"gun":0}""");

        var result = events.First(e => e.Type == "tool_result").Data;
        result.GetProperty("error").GetString().Should().Contain("finansal");
        events.Single(e => e.Type == "delta" && e.Data.GetProperty("text").GetString()!.Contains("hata")).Should().NotBeNull();
    }

    [Fact]
    public async Task Sales_rep_cannot_reach_foreign_customer_through_tools()
    {
        var rep = await factory.CreateClientAsAsync(DemoUsers.SalesRep);
        var me = await rep.GetJsonAsync<ErpAssistant.Application.Auth.UserInfo>("/api/v1/auth/me");
        var all = await factory.Admin.GetJsonAsync<PagedResult<ErpAssistant.Application.Customers.CustomerListItem>>("/api/v1/customers?pageSize=100");
        var foreign = all.Items.First(c => c.SalesRepresentativeId != me.SalesRepresentativeId);
        var own = all.Items.First(c => c.SalesRepresentativeId == me.SalesRepresentativeId);

        var denied = await rep.AskAsync($$"""TOOL:musteri_siparisleri:{"musteri":"{{foreign.Code}}","durum":"Tum"}""");
        var allowed = await rep.AskAsync($$"""TOOL:musteri_siparisleri:{"musteri":"{{own.Code}}","durum":"Tum"}""");

        denied.First(e => e.Type == "tool_result").Data.GetProperty("error").GetString().Should().Contain("görme yetkiniz yok");
        allowed.First(e => e.Type == "tool_result").Data.GetProperty("error").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task Plain_greeting_needs_no_tool()
    {
        var events = await factory.Admin.AskAsync("Merhaba");

        events.Should().NotContain(e => e.Type == "tool_call");
        events.Should().Contain(e => e.Type == "done");
    }
}
