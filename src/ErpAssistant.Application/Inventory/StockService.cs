using ErpAssistant.Application.Abstractions;
using ErpAssistant.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace ErpAssistant.Application.Inventory;

public sealed class StockService(IErpDbContext db)
{
    public async Task<PagedResult<StockItem>> ListAsync(StockQuery q, CancellationToken ct)
    {
        var query =
            from b in db.StockBalances.AsNoTracking()
            join p in db.Products on b.ProductId equals p.Id
            join w in db.Warehouses on b.WarehouseId equals w.Id
            select new { b, p, w };

        if (!string.IsNullOrWhiteSpace(q.Warehouse))
            query = query.Where(x => x.w.Code == q.Warehouse);
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(x => x.p.Name.Contains(q.Search) || x.p.Sku.Contains(q.Search));
        if (q.CriticalOnly)
            query = query.Where(x => x.b.Quantity < x.p.ReorderLevel);

        return await query
            .OrderBy(x => x.b.Quantity - x.p.ReorderLevel)
            .ThenBy(x => x.p.Sku)
            .Select(x => new StockItem(x.p.Id, x.p.Sku, x.p.Name, x.w.Code, x.w.Name, x.b.Quantity, x.p.ReorderLevel,
                x.b.Quantity < x.p.ReorderLevel))
            .ToPagedAsync(q, ct);
    }

    public async Task<IReadOnlyList<WarehouseItem>> WarehousesAsync(CancellationToken ct) =>
        await db.Warehouses.AsNoTracking().OrderBy(w => w.Code)
            .Select(w => new WarehouseItem(w.Id, w.Code, w.Name, w.City))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<StockMovementItem>> MovementsAsync(int productId, int take, CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(p => p.Id == productId, ct))
            throw new NotFoundException("Ürün", productId);

        var rows = await db.StockMovements.AsNoTracking()
            .Where(m => m.ProductId == productId)
            .OrderByDescending(m => m.OccurredAt)
            .Take(Math.Clamp(take, 1, 200))
            .Select(m => new { m.OccurredAt, m.Warehouse.Code, m.Type, m.Quantity, m.Reference })
            .ToListAsync(ct);

        return rows.Select(m => new StockMovementItem(m.OccurredAt, m.Code, m.Type.ToString(), m.Quantity, m.Reference)).ToList();
    }
}
