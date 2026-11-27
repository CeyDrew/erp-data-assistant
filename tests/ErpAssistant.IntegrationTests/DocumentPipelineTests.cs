using System.Net.Http.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ErpAssistant.Application.Common;
using ErpAssistant.Application.Documents;
using ErpAssistant.Domain.Documents;
using ErpAssistant.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace ErpAssistant.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class DocumentPipelineTests(ErpApiFactory factory)
{
    private static MultipartFormDataContent Form(string fileName, byte[] content, string department)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent { { file, "file", fileName }, { new StringContent(department), "department" } };
    }

    private static async Task<DocumentItem> UploadAsync(HttpClient client, string fileName, string text, string department)
    {
        var response = await client.PostAsync("/api/v1/documents", Form(fileName, Encoding.UTF8.GetBytes(text), department));
        response.StatusCode.Should().Be(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<DocumentItem>(HttpExtensions.Json))!;
    }

    private async Task<DocumentItem> WaitAsync(int id, Func<DocumentItem, bool> until)
    {
        for (var i = 0; i < 120; i++)
        {
            var doc = await factory.Admin.GetJsonAsync<DocumentItem>($"/api/v1/documents/{id}");
            if (until(doc)) return doc;
            await Task.Delay(500);
        }
        throw new TimeoutException($"Doküman {id} beklenen duruma gelmedi.");
    }

    [Fact]
    public async Task Uploaded_document_is_processed_and_cited_in_answers()
    {
        var doc = await UploadAsync(factory.Admin, "test-garanti.txt",
            "Garanti şartları. Ayıpsız ürünlerde iade süresi teslim tarihinden itibaren on dört gündür. " +
            "Pompa garanti süresi on sekiz aydır.", "KALITE");
        doc.Status.Should().Be(DocumentStatus.Pending);

        var ready = await WaitAsync(doc.Id, d => d.Status is DocumentStatus.Ready or DocumentStatus.Failed);
        ready.Status.Should().Be(DocumentStatus.Ready, ready.Error);
        ready.ChunkCount.Should().BeGreaterThan(0);

        var events = await factory.Admin.AskAsync("""TOOL:dokumanda_ara:{"soru":"ayıpsız ürünlerde iade süresi kaç gün"}""");
        events.First(e => e.Type == "tool_result").Data.GetProperty("rowCount").GetInt32().Should().BeGreaterThan(0);
        var sources = events.Single(e => e.Type == "sources").Data.GetProperty("sources");
        sources.EnumerateArray().Should().Contain(s => s.GetProperty("fileName").GetString() == "test-garanti.txt");
    }

    [Fact]
    public async Task Department_filter_applies_to_vector_search()
    {
        var doc = await UploadAsync(factory.Admin, "depo-ozel.txt",
            "Depo forklift operatörleri vardiya başında kask ve reflektörlü yelek kontrolü yapar.", "DEPO");
        await WaitAsync(doc.Id, d => d.Status == DocumentStatus.Ready);

        var rep = await factory.CreateClientAsAsync(DemoUsers.SalesRep);
        var warehouse = await factory.CreateClientAsAsync(DemoUsers.Warehouse);
        const string ask = """TOOL:dokumanda_ara:{"soru":"forklift operatörleri vardiya kask yelek"}""";

        var repEvents = await rep.AskAsync(ask);
        var whEvents = await warehouse.AskAsync(ask);

        repEvents.Should().NotContain(e => e.Type == "sources" &&
            e.Data.GetProperty("sources").EnumerateArray().Any(s => s.GetProperty("fileName").GetString() == "depo-ozel.txt"));
        whEvents.Single(e => e.Type == "sources").Data.GetProperty("sources").EnumerateArray()
            .Should().Contain(s => s.GetProperty("fileName").GetString() == "depo-ozel.txt");

        // Liste uç noktası da aynı filtreyi uygular.
        var list = await rep.GetJsonAsync<PagedResult<DocumentItem>>("/api/v1/documents?pageSize=100");
        list.Items.Should().NotContain(d => d.Id == doc.Id);
    }

    [Fact]
    public async Task Users_cannot_upload_to_departments_they_cannot_read()
    {
        var warehouse = await factory.CreateClientAsAsync(DemoUsers.Warehouse);

        var response = await warehouse.PostAsync("/api/v1/documents", Form("x.txt", "metin"u8.ToArray(), "SATIS"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unsupported_file_type_is_rejected()
    {
        var response = await factory.Admin.PostAsync("/api/v1/documents", Form("script.exe", [1, 2, 3], "KALITE"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Corrupt_document_fails_after_retries_and_goes_to_dead_letter()
    {
        var response = await factory.Admin.PostAsync("/api/v1/documents",
            Form("bozuk.pdf", "%PDF-1.7 bu gerçek bir pdf değil"u8.ToArray(), "KALITE"));
        var doc = (await response.Content.ReadFromJsonAsync<DocumentItem>(HttpExtensions.Json))!;

        var failed = await WaitAsync(doc.Id, d => d.Status == DocumentStatus.Failed);

        failed.Attempts.Should().Be(3);
        failed.Error.Should().Contain("3 denemede");
        (await factory.DeadLetterCountAsync()).Should().BeGreaterThan(0);
    }
}
