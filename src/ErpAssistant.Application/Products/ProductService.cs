using ErpAssistant.Application.Abstractions;
using ErpAssistant.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace ErpAssistant.Application.Products;

public sealed class ProductService(IErpDbContext db, ICurrentUser user)
{
    public async Task<PagedResult<ProductListItem>> ListAsync(ProductQuery q, CancellationToken ct)
    {
        var query = db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(p => p.Name.Contains(q.Search) || p.Sku.Contains(q.Search));
        if (q.CategoryId is { } categoryId)
            query = query.Where(p => p.CategoryId == categoryId);
        if (q.IsActive is { } active)
            query = query.Where(p => p.IsActive == active);

        var canSeePrices = user.CanSeeFinancials;
        return await query
            .OrderBy(p => p.Sku)
            .Select(p => new ProductListItem(
                p.Id, p.Sku, p.Name, p.Category.Name, p.Unit,
                canSeePrices ? p.UnitPrice : null,
                p.IsActive,
                db.StockBalances.Where(b => b.ProductId == p.Id).Sum(b => (int?)b.Quantity) ?? 0))
            .ToPagedAsync(q, ct);
    }

    public async Task<ProductDetail> GetAsync(int id, CancellationToken ct)
    {
        var canSeePrices = user.CanSeeFinancials;
        var product = await db.Products.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id, p.Sku, p.Name, p.CategoryId, Category = p.Category.Name, p.Unit, p.UnitPrice, p.UnitCost,
                p.ReorderLevel, p.IsActive,
            })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Ürün", id);

        var stock = await db.StockBalances.AsNoTracking()
            .Where(b => b.ProductId == id)
            .Join(db.Warehouses, b => b.WarehouseId, w => w.Id, (b, w) => new WarehouseStock(w.Id, w.Code, w.Name, b.Quantity))
            .OrderBy(s => s.WarehouseCode)
            .ToListAsync(ct);

        return new ProductDetail(product.Id, product.Sku, product.Name, product.CategoryId, product.Category, product.Unit,
            canSeePrices ? product.UnitPrice : null, canSeePrices ? product.UnitCost : null,
            product.ReorderLevel, product.IsActive, stock);
    }

    public async Task<IReadOnlyList<CategoryItem>> CategoriesAsync(CancellationToken ct) =>
        await db.ProductCategories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryItem(c.Id, c.Name, c.Products.Count))
            .ToListAsync(ct);
}
