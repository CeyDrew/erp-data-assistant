using ErpAssistant.Application.Common;
using ErpAssistant.Application.Inventory;
using Microsoft.AspNetCore.Mvc;

namespace ErpAssistant.Api.Controllers;

public sealed class StockController(StockService stock) : ApiControllerBase
{
    /// <summary>Ürün × depo bakiyeleri; kritik olanlar önce.</summary>
    [HttpGet]
    public Task<PagedResult<StockItem>> List([FromQuery] StockQuery query, CancellationToken ct) => stock.ListAsync(query, ct);

    [HttpGet("warehouses")]
    public Task<IReadOnlyList<WarehouseItem>> Warehouses(CancellationToken ct) => stock.WarehousesAsync(ct);
}
