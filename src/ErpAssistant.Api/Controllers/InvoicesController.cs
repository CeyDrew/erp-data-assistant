using ErpAssistant.Application.Common;
using ErpAssistant.Application.Invoices;
using Microsoft.AspNetCore.Mvc;

namespace ErpAssistant.Api.Controllers;

public sealed class InvoicesController(InvoiceService invoices) : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<InvoiceListItem>> List([FromQuery] InvoiceQuery query, CancellationToken ct) =>
        invoices.ListAsync(query, ct);

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<InvoiceDetail> Get(int id, CancellationToken ct) => invoices.GetAsync(id, ct);
}
