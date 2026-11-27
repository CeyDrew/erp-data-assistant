using ErpAssistant.Application.Common;
using ErpAssistant.Application.Invoices;
using ErpAssistant.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpAssistant.Api.Controllers;

[Authorize(Policy = Policies.Financials)]
public sealed class InvoicesController(InvoiceService invoices) : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<InvoiceListItem>> List([FromQuery] InvoiceQuery query, CancellationToken ct) =>
        invoices.ListAsync(query, ct);

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<InvoiceDetail> Get(int id, CancellationToken ct) => invoices.GetAsync(id, ct);
}
