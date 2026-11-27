using FluentValidation;
using ErpAssistant.Application.Common;

namespace ErpAssistant.Application.Inventory;

public sealed record StockQuery : PageQuery
{
    public string? Warehouse { get; init; }
    public string? Search { get; init; }
    public bool CriticalOnly { get; init; }
}

public sealed class StockQueryValidator : PageQueryValidator<StockQuery>
{
    public StockQueryValidator()
    {
        RuleFor(x => x.Warehouse).MaximumLength(16);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

public sealed record StockItem(
    int ProductId, string Sku, string ProductName, string WarehouseCode, string WarehouseName,
    int Quantity, int ReorderLevel, bool IsCritical);

public sealed record WarehouseItem(int Id, string Code, string Name, string City);

public sealed record StockMovementItem(DateTime OccurredAt, string WarehouseCode, string Type, int Quantity, string? Reference);
