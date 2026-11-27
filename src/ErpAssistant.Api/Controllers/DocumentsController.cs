using ErpAssistant.Application.Abstractions;
using ErpAssistant.Application.Common;
using ErpAssistant.Application.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpAssistant.Api.Controllers;

public sealed class DocumentsController(DocumentService documents) : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<DocumentItem>> List([FromQuery] DocumentQuery query, CancellationToken ct) =>
        documents.ListAsync(query, ct);

    [HttpGet("{id:int}")]
    public Task<DocumentItem> Get(int id, CancellationToken ct) => documents.GetAsync(id, ct);

    [HttpGet("departments")]
    public Task<IReadOnlyList<DepartmentItem>> Departments(CancellationToken ct) => documents.DepartmentsAsync(ct);

    /// <summary>PDF, DOCX, XLSX, TXT veya MD yükler; işleme kuyruğa alınır (202).</summary>
    [HttpPost]
    [RequestSizeLimit(DocumentService.MaxBytes + 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] string department, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var item = await documents.UploadAsync(stream, file.FileName, file.Length, department, ct);
        return AcceptedAtAction(nameof(Get), new { id = item.Id, version = "1" }, item);
    }

    [HttpPost("{id:int}/reprocess")]
    [Authorize(Roles = Roles.Admin)]
    public Task<DocumentItem> Reprocess(int id, CancellationToken ct) => documents.ReprocessAsync(id, ct);

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await documents.DeleteAsync(id, ct);
        return NoContent();
    }
}
