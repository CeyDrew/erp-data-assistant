using ErpAssistant.Application.Common;
using ErpAssistant.Domain.Sales;
using FluentValidation;

namespace ErpAssistant.Application.Orders;

public sealed record OrderQuery : PageQuery
{
    public int? CustomerId { get; init; }
    public OrderStatus? Status { get; init; }
    public bool? OpenOnly { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string? Search { get; init; }
}

public sealed class OrderQueryValidator : PageQueryValidator<OrderQuery>
{
    public OrderQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(32);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From <= x.To)
            .WithName("From").WithMessage("Başlangıç tarihi bitişten sonra olamaz.");
    }
}

public sealed record OrderListItem(
    int Id, string OrderNumber, int CustomerId, string CustomerName, DateTime OrderDate, OrderStatus Status,
    decimal? TotalAmount, string WarehouseCode, int LineCount);

public sealed record OrderDetail(
    int Id, string OrderNumber, int CustomerId, string CustomerName, string SalesRepresentative, DateTime OrderDate,
    OrderStatus Status, DateTime? ShippedAt, string WarehouseCode, decimal? TotalAmount, string? InvoiceNumber,
    IReadOnlyList<OrderLineItem> Lines);

public sealed record OrderLineItem(
    int ProductId, string Sku, string ProductName, string Unit, int Quantity, decimal? UnitPrice, decimal? DiscountRate,
    decimal? LineTotal);
