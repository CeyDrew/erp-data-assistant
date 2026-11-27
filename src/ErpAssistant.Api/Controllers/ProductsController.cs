using ErpAssistant.Application.Common;
using ErpAssistant.Application.Inventory;
using ErpAssistant.Application.Products;
using Microsoft.AspNetCore.Mvc;

namespace ErpAssistant.Api.Controllers;

public sealed class ProductsController(ProductService products, StockService stock) : ApiControllerBase
{
    /// <summary>Ürünleri sayfalı listeler.</summary>
    [HttpGet]
    public Task<PagedResult<ProductListItem>> List([FromQuery] ProductQuery query, CancellationToken ct) =>
        products.ListAsync(query, ct);

    /// <summary>Ürün detayı ve depo bazında stok.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ProductDetail> Get(int id, CancellationToken ct) => products.GetAsync(id, ct);

    /// <summary>Ürünün son stok hareketleri.</summary>
    [HttpGet("{id:int}/movements")]
    public Task<IReadOnlyList<StockMovementItem>> Movements(int id, [FromQuery] int take = 50, CancellationToken ct = default) =>
        stock.MovementsAsync(id, take, ct);

    [HttpGet("categories")]
    public Task<IReadOnlyList<CategoryItem>> Categories(CancellationToken ct) => products.CategoriesAsync(ct);
}
