using System.Globalization;
using ErpAssistant.Application.Abstractions;
using ErpAssistant.Application.Common;
using ErpAssistant.Domain.Billing;
using ErpAssistant.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace ErpAssistant.Application.Reports;

/// <summary>
/// Raporlama sorguları. Panel, AI asistanı ve MCP araçları aynı metotları kullanır; satır yetkisi
/// DbContext filtrelerinden, finansal görünürlük <see cref="ICurrentUser"/>'dan gelir.
/// </summary>
public sealed class ReportService(IErpDbContext db, ICurrentUser user, TimeProvider clock)
{
    public const int MaxRows = 50;

    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private IQueryable<Order> RevenueOrders(DateTime from, DateTime to) =>
        db.Orders.AsNoTracking().Where(o =>
            o.OrderDate >= from && o.OrderDate < to.Date.AddDays(1) &&
            o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Draft);

    public async Task<SalesSummary> SalesSummaryAsync(DateTime from, DateTime to, SalesGrouping groupBy, CancellationToken ct)
    {
        EnsureFinancials();
        var orders = RevenueOrders(from, to);

        var totals = await orders.GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Revenue = g.Sum(o => o.TotalAmount) })
            .FirstOrDefaultAsync(ct);

        List<SalesSummaryRow> rows = groupBy switch
        {
            SalesGrouping.Day => (await orders
                    .GroupBy(o => o.OrderDate.Date)
                    .Select(g => new { g.Key, Count = g.Count(), Revenue = g.Sum(o => o.TotalAmount) })
                    .OrderBy(x => x.Key)
                    .ToListAsync(ct))
                .Select(x => new SalesSummaryRow(x.Key.ToString("yyyy-MM-dd"), x.Key.ToString("d MMMM yyyy", Tr), x.Count, x.Revenue))
                .ToList(),

            SalesGrouping.Month => (await orders
                    .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                    .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count(), Revenue = g.Sum(o => o.TotalAmount) })
                    .OrderBy(x => x.Year).ThenBy(x => x.Month)
                    .ToListAsync(ct))
                .Select(x => new SalesSummaryRow($"{x.Year}-{x.Month:D2}",
                    new DateTime(x.Year, x.Month, 1).ToString("MMMM yyyy", Tr), x.Count, x.Revenue))
                .ToList(),

            SalesGrouping.Category => (await orders
                    .SelectMany(o => o.Lines)
                    .GroupBy(l => new { l.Product.CategoryId, l.Product.Category.Name })
                    .Select(g => new
                    {
                        g.Key.CategoryId, g.Key.Name,
                        Count = g.Select(l => l.OrderId).Distinct().Count(),
                        Revenue = g.Sum(l => l.LineTotal),
                    })
                    .OrderByDescending(x => x.Revenue)
                    .ToListAsync(ct))
                .Select(x => new SalesSummaryRow(x.CategoryId.ToString(), x.Name, x.Count, x.Revenue))
                .ToList(),

            SalesGrouping.SalesRepresentative => (await orders
                    .GroupBy(o => new { o.SalesRepresentativeId, o.SalesRepresentative.FullName })
                    .Select(g => new { g.Key.SalesRepresentativeId, g.Key.FullName, Count = g.Count(), Revenue = g.Sum(o => o.TotalAmount) })
                    .OrderByDescending(x => x.Revenue)
                    .ToListAsync(ct))
                .Select(x => new SalesSummaryRow(x.SalesRepresentativeId.ToString(), x.FullName, x.Count, x.Revenue))
                .ToList(),

            SalesGrouping.Customer => (await orders
                    .GroupBy(o => new { o.CustomerId, o.Customer.Name })
                    .Select(g => new { g.Key.CustomerId, g.Key.Name, Count = g.Count(), Revenue = g.Sum(o => o.TotalAmount) })
                    .OrderByDescending(x => x.Revenue)
                    .Take(MaxRows + 1)
                    .ToListAsync(ct))
                .Select(x => new SalesSummaryRow(x.CustomerId.ToString(), x.Name, x.Count, x.Revenue))
                .ToList(),

            _ => throw new ArgumentOutOfRangeException(nameof(groupBy)),
        };

        var truncated = rows.Count > MaxRows;
        if (truncated) rows = rows.Take(MaxRows).ToList();

        var count = totals?.Count ?? 0;
        var revenue = totals?.Revenue ?? 0;
        return new SalesSummary(from, to, groupBy, count, revenue, count == 0 ? 0 : Math.Round(revenue / count, 2), rows, truncated);
    }

    public async Task<IReadOnlyList<TopProductRow>> TopProductsAsync(int top, DateTime from, DateTime to,
        TopProductMetric metric, int? categoryId, CancellationToken ct)
    {
        top = Math.Clamp(top, 1, MaxRows);
        var lines = RevenueOrders(from, to).SelectMany(o => o.Lines);
        if (categoryId is { } cat)
            lines = lines.Where(l => l.Product.CategoryId == cat);

        var grouped = lines
            .GroupBy(l => new { l.ProductId, l.Product.Sku, l.Product.Name, Category = l.Product.Category.Name })
            .Select(g => new { g.Key.ProductId, g.Key.Sku, g.Key.Name, g.Key.Category, Quantity = g.Sum(l => l.Quantity), Revenue = g.Sum(l => l.LineTotal) });

        grouped = metric == TopProductMetric.Quantity
            ? grouped.OrderByDescending(x => x.Quantity).ThenBy(x => x.ProductId)
            : grouped.OrderByDescending(x => x.Revenue).ThenBy(x => x.ProductId);

        var rows = await grouped.Take(top).ToListAsync(ct);
        var showRevenue = user.CanSeeFinancials;
        return rows.Select((x, i) => new TopProductRow(i + 1, x.ProductId, x.Sku, x.Name, x.Category, x.Quantity,
            showRevenue ? x.Revenue : 0)).ToList();
    }

    public async Task<OverdueSummary> OverdueInvoicesAsync(int minDays, int? customerId, CancellationToken ct)
    {
        EnsureFinancials();
        var today = clock.GetUtcNow().UtcDateTime.Date;
        var threshold = today.AddDays(-Math.Max(0, minDays));

        var query = db.Invoices.AsNoTracking()
            .Where(i => (i.Status == InvoiceStatus.Open || i.Status == InvoiceStatus.PartiallyPaid) && i.DueDate < threshold);
        if (customerId is { } cid)
            query = query.Where(i => i.CustomerId == cid);

        var totals = await query.GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Sum = g.Sum(i => i.TotalAmount - i.PaidAmount) })
            .FirstOrDefaultAsync(ct);

        var rows = await query
            .OrderBy(i => i.DueDate)
            .Take(MaxRows)
            .Select(i => new OverdueInvoiceRow(i.Id, i.InvoiceNumber, i.CustomerId, i.Customer.Name,
                i.SalesRepresentative.FullName, i.DueDate, 0,
                i.TotalAmount - i.PaidAmount))
            .ToListAsync(ct);

        rows = rows.Select(r => r with { DaysOverdue = (today - r.DueDate.Date).Days }).ToList();
        var count = totals?.Count ?? 0;
        return new OverdueSummary(minDays, count, totals?.Sum ?? 0, rows, count > rows.Count);
    }

    public async Task<DashboardSummary> DashboardAsync(CancellationToken ct)
    {
        var today = clock.GetUtcNow().UtcDateTime.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var lastMonthStart = monthStart.AddMonths(-1);
        var yearAgo = monthStart.AddMonths(-11);

        var openOrders = await db.Orders.CountAsync(o => o.Status == OrderStatus.Draft || o.Status == OrderStatus.Confirmed, ct);
        var critical = await db.StockBalances
            .Join(db.Products, b => b.ProductId, p => p.Id, (b, p) => new { b.Quantity, p.ReorderLevel })
            .CountAsync(x => x.Quantity < x.ReorderLevel, ct);
        var top = await TopProductsAsync(5, yearAgo, today, user.CanSeeFinancials ? TopProductMetric.Revenue : TopProductMetric.Quantity, null, ct);

        if (!user.CanSeeFinancials)
        {
            var ordersThisMonth = await db.Orders.CountAsync(o => o.OrderDate >= monthStart, ct);
            return new DashboardSummary(0, 0, ordersThisMonth, openOrders, 0, critical, [], top, []);
        }

        var thisMonth = await SalesSummaryAsync(monthStart, today, SalesGrouping.Month, ct);
        var lastMonth = await SalesSummaryAsync(lastMonthStart, monthStart.AddDays(-1), SalesGrouping.Month, ct);
        var monthly = await SalesSummaryAsync(yearAgo, today, SalesGrouping.Month, ct);
        var byCategory = await SalesSummaryAsync(yearAgo, today, SalesGrouping.Category, ct);
        var overdue = await OverdueInvoicesAsync(0, null, ct);

        return new DashboardSummary(thisMonth.Revenue, lastMonth.Revenue, thisMonth.OrderCount, openOrders,
            overdue.TotalOutstanding, critical, monthly.Rows, top, byCategory.Rows);
    }

    private void EnsureFinancials()
    {
        if (!user.CanSeeFinancials)
            throw new ForbiddenException("Bu rapor finansal veri içerir; rolünüzün erişimi yok.");
    }
}
