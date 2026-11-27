using ErpAssistant.Application.Reports;
using Microsoft.AspNetCore.Mvc;

namespace ErpAssistant.Api.Controllers;

public sealed class ReportsController(ReportService reports, TimeProvider clock) : ApiControllerBase
{
    [HttpGet("dashboard")]
    public Task<DashboardSummary> Dashboard(CancellationToken ct) => reports.DashboardAsync(ct);

    [HttpGet("sales-summary")]
    public Task<SalesSummary> SalesSummary(DateTime? from, DateTime? to, SalesGrouping groupBy = SalesGrouping.Month,
        CancellationToken ct = default)
    {
        var (f, t) = Range(from, to);
        return reports.SalesSummaryAsync(f, t, groupBy, ct);
    }

    [HttpGet("top-products")]
    public Task<IReadOnlyList<TopProductRow>> TopProducts(int top = 10, DateTime? from = null, DateTime? to = null,
        TopProductMetric metric = TopProductMetric.Revenue, int? categoryId = null, CancellationToken ct = default)
    {
        var (f, t) = Range(from, to);
        return reports.TopProductsAsync(top, f, t, metric, categoryId, ct);
    }

    [HttpGet("overdue-invoices")]
    public Task<OverdueSummary> Overdue(int minDays = 0, int? customerId = null, CancellationToken ct = default) =>
        reports.OverdueInvoicesAsync(minDays, customerId, ct);

    private (DateTime From, DateTime To) Range(DateTime? from, DateTime? to)
    {
        var today = clock.GetUtcNow().UtcDateTime.Date;
        var t = to?.Date ?? today;
        var f = from?.Date ?? t.AddMonths(-1);
        if (f > t) throw new FluentValidation.ValidationException("Başlangıç tarihi bitişten sonra olamaz.");
        if ((t - f).TotalDays > 3 * 366) throw new FluentValidation.ValidationException("Tarih aralığı en fazla 3 yıl olabilir.");
        return (f, t);
    }
}
