using ErpAssistant.Application.Common;
using ErpAssistant.Domain.Billing;
using FluentValidation;

namespace ErpAssistant.Application.Invoices;

public sealed record InvoiceQuery : PageQuery
{
    public int? CustomerId { get; init; }
    public InvoiceStatus? Status { get; init; }
    public bool OverdueOnly { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}

public sealed class InvoiceQueryValidator : PageQueryValidator<InvoiceQuery>
{
    public InvoiceQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From <= x.To)
            .WithName("From").WithMessage("Başlangıç tarihi bitişten sonra olamaz.");
    }
}

public sealed record InvoiceListItem(
    int Id, string InvoiceNumber, int OrderId, int CustomerId, string CustomerName, DateTime IssueDate, DateTime DueDate,
    decimal TotalAmount, decimal PaidAmount, decimal Outstanding, InvoiceStatus Status, int DaysOverdue);

public sealed record InvoiceDetail(
    int Id, string InvoiceNumber, int OrderId, string OrderNumber, int CustomerId, string CustomerName,
    DateTime IssueDate, DateTime DueDate, decimal NetAmount, decimal TaxAmount, decimal TotalAmount, decimal PaidAmount,
    InvoiceStatus Status, IReadOnlyList<PaymentItem> Payments);

public sealed record PaymentItem(DateTime PaidAt, decimal Amount, PaymentMethod Method);
