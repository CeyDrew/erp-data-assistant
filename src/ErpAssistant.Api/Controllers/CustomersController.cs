using ErpAssistant.Application.Common;
using ErpAssistant.Application.Customers;
using Microsoft.AspNetCore.Mvc;

namespace ErpAssistant.Api.Controllers;

public sealed class CustomersController(CustomerService customers) : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<CustomerListItem>> List([FromQuery] CustomerQuery query, CancellationToken ct) =>
        customers.ListAsync(query, ct);

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<CustomerDetail> Get(int id, CancellationToken ct) => customers.GetAsync(id, ct);
}
