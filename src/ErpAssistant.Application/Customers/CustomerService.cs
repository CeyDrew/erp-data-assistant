using ErpAssistant.Application.Abstractions;
using ErpAssistant.Application.Common;
using ErpAssistant.Domain.Billing;
using ErpAssistant.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace ErpAssistant.Application.Customers;

public sealed class CustomerService(IErpDbContext db)
{
    public async Task<PagedResult<CustomerListItem>> ListAsync(CustomerQuery q, CancellationToken ct)
    {
        var query = db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(c => c.Name.Contains(q.Search) || c.Code.Contains(q.Search) || c.TaxNumber == q.Search);
        if (!string.IsNullOrWhiteSpace(q.City))
            query = query.Where(c => c.City == q.City);
        if (q.SalesRepresentativeId is { } repId)
            query = query.Where(c => c.SalesRepresentativeId == repId);

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new CustomerListItem(c.Id, c.Code, c.Name, c.City, c.TaxNumber, c.SalesRepresentativeId,
                c.SalesRepresentative.FullName))
            .ToPagedAsync(q, ct);
    }

    public async Task<CustomerDetail> GetAsync(int id, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id, c.Code, c.Name, c.City, c.TaxNumber, c.Email, c.Phone, c.CreditLimit, c.SalesRepresentativeId,
                Rep = c.SalesRepresentative.FullName,
            })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Müşteri", id);

        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == id)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Open = g.Count(o => o.Status == OrderStatus.Draft || o.Status == OrderStatus.Confirmed),
                Revenue = g.Where(o => o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Draft).Sum(o => (decimal?)o.TotalAmount) ?? 0,
                Last = g.Max(o => (DateTime?)o.OrderDate),
            })
            .FirstOrDefaultAsync(ct);

        var outstanding = await db.Invoices.AsNoTracking()
            .Where(i => i.CustomerId == id && (i.Status == InvoiceStatus.Open || i.Status == InvoiceStatus.PartiallyPaid))
            .SumAsync(i => (decimal?)(i.TotalAmount - i.PaidAmount), ct) ?? 0;

        return new CustomerDetail(customer.Id, customer.Code, customer.Name, customer.City, customer.TaxNumber,
            customer.Email, customer.Phone, customer.CreditLimit, customer.SalesRepresentativeId, customer.Rep,
            orders?.Count ?? 0, orders?.Open ?? 0, orders?.Revenue ?? 0, outstanding, orders?.Last);
    }
}
