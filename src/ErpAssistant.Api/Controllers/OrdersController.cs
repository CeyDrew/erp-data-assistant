using ErpAssistant.Application.Common;
using ErpAssistant.Application.Orders;
using Microsoft.AspNetCore.Mvc;

namespace ErpAssistant.Api.Controllers;

public sealed class OrdersController(OrderService orders) : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<OrderListItem>> List([FromQuery] OrderQuery query, CancellationToken ct) =>
        orders.ListAsync(query, ct);

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<OrderDetail> Get(int id, CancellationToken ct) => orders.GetAsync(id, ct);
}
