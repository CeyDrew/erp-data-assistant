using FluentValidation;
using ErpAssistant.Application.Common;

namespace ErpAssistant.Application.Products;

public sealed record ProductQuery : PageQuery
{
    public string? Search { get; init; }
    public int? CategoryId { get; init; }
    public bool? IsActive { get; init; }
}

public sealed class ProductQueryValidator : PageQueryValidator<ProductQuery>
{
    public ProductQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

public sealed record ProductListItem(
    int Id, string Sku, string Name, string Category, string Unit, decimal? UnitPrice, bool IsActive, int TotalStock);

public sealed record ProductDetail(
    int Id, string Sku, string Name, int CategoryId, string Category, string Unit, decimal? UnitPrice, decimal? UnitCost,
    int ReorderLevel, bool IsActive, IReadOnlyList<WarehouseStock> Stock);

public sealed record WarehouseStock(int WarehouseId, string WarehouseCode, string WarehouseName, int Quantity);

public sealed record CategoryItem(int Id, string Name, int ProductCount);
