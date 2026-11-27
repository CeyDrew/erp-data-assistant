namespace ErpAssistant.Application.Reports;

public enum SalesGrouping
{
    Day,
    Month,
    Category,
    SalesRepresentative,
    Customer,
}

public enum TopProductMetric
{
    Revenue,
    Quantity,
}

public sealed record SalesSummaryRow(string Key, string Label, int OrderCount, decimal Revenue);

public sealed record SalesSummary(
    DateTime From, DateTime To, SalesGrouping GroupBy, int OrderCount, decimal Revenue, decimal AverageOrderValue,
    IReadOnlyList<SalesSummaryRow> Rows, bool Truncated);

public sealed record TopProductRow(int Rank, int ProductId, string Sku, string Name, string Category, int Quantity, decimal Revenue);

public sealed record OverdueInvoiceRow(
    int InvoiceId, string InvoiceNumber, int CustomerId, string CustomerName, string SalesRepresentative,
    DateTime DueDate, int DaysOverdue, decimal Outstanding);

public sealed record OverdueSummary(int MinDays, int InvoiceCount, decimal TotalOutstanding, IReadOnlyList<OverdueInvoiceRow> Rows, bool Truncated);

public sealed record DashboardSummary(
    decimal RevenueThisMonth, decimal RevenueLastMonth, int OrdersThisMonth, int OpenOrders, decimal Overdue,
    int CriticalStockItems, IReadOnlyList<SalesSummaryRow> MonthlyRevenue, IReadOnlyList<TopProductRow> TopProducts,
    IReadOnlyList<SalesSummaryRow> RevenueByCategory);
