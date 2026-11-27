using ErpAssistant.Application.Abstractions;
using ErpAssistant.Application.Common;
using ErpAssistant.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace ErpAssistant.Application.Invoices;

public sealed class InvoiceService(IErpDbContext db, TimeProvider clock)
{
    public async Task<PagedResult<InvoiceListItem>> ListAsync(InvoiceQuery q, CancellationToken ct)
    {
        var today = clock.GetUtcNow().UtcDateTime.Date;
        var query = db.Invoices.AsNoTracking();

        if (q.CustomerId is { } customerId)
            query = query.Where(i => i.CustomerId == customerId);
        if (q.Status is { } status)
            query = query.Where(i => i.Status == status);
        if (q.OverdueOnly)
            query = query.Where(i => (i.Status == InvoiceStatus.Open || i.Status == InvoiceStatus.PartiallyPaid) && i.DueDate < today);
        if (q.From is { } from)
            query = query.Where(i => i.IssueDate >= from);
        if (q.To is { } to)
            query = query.Where(i => i.IssueDate < to.Date.AddDays(1));

        var page = await query
            .OrderByDescending(i => i.IssueDate)
            .ThenByDescending(i => i.Id)
            .Select(i => new InvoiceListItem(i.Id, i.InvoiceNumber, i.OrderId, i.CustomerId, i.Customer.Name, i.IssueDate,
                i.DueDate, i.TotalAmount, i.PaidAmount, i.TotalAmount - i.PaidAmount, i.Status, 0))
            .ToPagedAsync(q, ct);

        // Gecikme günü sağlayıcıdan bağımsız kalsın diye bellekte hesaplanır (sayfa en fazla 100 satır).
        return page with
        {
            Items = page.Items.Select(i => i with { DaysOverdue = DaysOverdue(i.Status, i.DueDate, today) }).ToList(),
        };
    }

    public async Task<InvoiceDetail> GetAsync(int id, CancellationToken ct)
    {
        var invoice = await db.Invoices.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new InvoiceDetail(i.Id, i.InvoiceNumber, i.OrderId, i.Order.OrderNumber, i.CustomerId,
                i.Customer.Name, i.IssueDate, i.DueDate, i.NetAmount, i.TaxAmount, i.TotalAmount, i.PaidAmount, i.Status,
                i.Payments.OrderBy(p => p.PaidAt).Select(p => new PaymentItem(p.PaidAt, p.Amount, p.Method)).ToList()))
            .FirstOrDefaultAsync(ct);

        return invoice ?? throw new NotFoundException("Fatura", id);
    }

    public static int DaysOverdue(InvoiceStatus status, DateTime dueDate, DateTime today) =>
        status is InvoiceStatus.Open or InvoiceStatus.PartiallyPaid && dueDate < today ? (today - dueDate.Date).Days : 0;
}
