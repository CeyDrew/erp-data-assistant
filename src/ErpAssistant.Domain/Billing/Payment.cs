using ErpAssistant.Domain.Common;

namespace ErpAssistant.Domain.Billing;

public sealed class Payment : Entity
{
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }
    public PaymentMethod Method { get; set; }
}
