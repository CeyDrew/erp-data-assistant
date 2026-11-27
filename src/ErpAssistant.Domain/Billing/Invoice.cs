using ErpAssistant.Domain.Common;
using ErpAssistant.Domain.Organization;
using ErpAssistant.Domain.Sales;

namespace ErpAssistant.Domain.Billing;

public sealed class Invoice : Entity
{
    public const decimal VatRate = 0.20m;

    public required string InvoiceNumber { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int SalesRepresentativeId { get; set; }
    public SalesRepresentative SalesRepresentative { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public InvoiceStatus Status { get; set; }

    public List<Payment> Payments { get; set; } = [];

    public decimal Outstanding => TotalAmount - PaidAmount;

    public static Invoice ForOrder(string invoiceNumber, Order order, DateTime issueDate, int paymentTermDays)
    {
        if (order.Status is OrderStatus.Draft or OrderStatus.Cancelled)
            throw new DomainException("Taslak veya iptal edilmiş sipariş faturalanamaz.");

        var tax = Math.Round(order.TotalAmount * VatRate, 2, MidpointRounding.AwayFromZero);
        return new Invoice
        {
            InvoiceNumber = invoiceNumber,
            OrderId = order.Id,
            Order = order,
            CustomerId = order.CustomerId,
            SalesRepresentativeId = order.SalesRepresentativeId,
            IssueDate = issueDate,
            DueDate = issueDate.AddDays(paymentTermDays),
            NetAmount = order.TotalAmount,
            TaxAmount = tax,
            TotalAmount = order.TotalAmount + tax,
            Status = InvoiceStatus.Open,
        };
    }

    public Payment RegisterPayment(decimal amount, DateTime paidAt, PaymentMethod method)
    {
        if (Status is InvoiceStatus.Cancelled or InvoiceStatus.Paid)
            throw new DomainException("Kapalı faturaya tahsilat girilemez.");
        if (amount <= 0)
            throw new DomainException("Tahsilat tutarı pozitif olmalı.");
        if (amount > Outstanding)
            throw new DomainException("Tahsilat, kalan borçtan büyük olamaz.");

        var payment = new Payment { Invoice = this, Amount = amount, PaidAt = paidAt, Method = method };
        Payments.Add(payment);
        PaidAmount += amount;
        Status = PaidAmount == TotalAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
        return payment;
    }
}
