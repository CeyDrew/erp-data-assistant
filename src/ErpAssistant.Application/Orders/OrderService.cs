using ErpAssistant.Application.Abstractions;
using ErpAssistant.Application.Common;
using ErpAssistant.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace ErpAssistant.Application.Orders;

public sealed class OrderService(IErpDbContext db, ICurrentUser user)
{
    public async Task<PagedResult<OrderListItem>> ListAsync(OrderQuery q, CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking();

        if (q.CustomerId is { } customerId)
            query = query.Where(o => o.CustomerId == customerId);
        if (q.Status is { } status)
            query = query.Where(o => o.Status == status);
        if (q.OpenOnly == true)
            query = query.Where(o => o.Status == OrderStatus.Draft || o.Status == OrderStatus.Confirmed);
        if (q.From is { } from)
            query = query.Where(o => o.OrderDate >= from);
        if (q.To is { } to)
            query = query.Where(o => o.OrderDate < to.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(o => o.OrderNumber.Contains(q.Search));

        var canSeePrices = user.CanSeeFinancials;
        return await query
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new OrderListItem(o.Id, o.OrderNumber, o.CustomerId, o.Customer.Name, o.OrderDate, o.Status,
                canSeePrices ? o.TotalAmount : null, o.Warehouse.Code, o.Lines.Count))
            .ToPagedAsync(q, ct);
    }

    public async Task<OrderDetail> GetAsync(int id, CancellationToken ct)
    {
        var canSeePrices = user.CanSeeFinancials;
        var order = await db.Orders.AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new OrderDetail(
                o.Id, o.OrderNumber, o.CustomerId, o.Customer.Name, o.SalesRepresentative.FullName, o.OrderDate, o.Status,
                o.ShippedAt, o.Warehouse.Code, canSeePrices ? o.TotalAmount : null,
                canSeePrices && o.Invoice != null ? o.Invoice.InvoiceNumber : null,
                o.Lines.OrderBy(l => l.Id).Select(l => new OrderLineItem(
                    l.ProductId, l.Product.Sku, l.Product.Name, l.Product.Unit, l.Quantity,
                    canSeePrices ? l.UnitPrice : null, canSeePrices ? l.DiscountRate : null,
                    canSeePrices ? l.LineTotal : null)).ToList()))
            .FirstOrDefaultAsync(ct);

        return order ?? throw new NotFoundException("Sipariş", id);
    }
}
