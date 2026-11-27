namespace ErpAssistant.Domain.Billing;

public enum InvoiceStatus
{
    Open = 0,
    PartiallyPaid = 1,
    Paid = 2,
    Cancelled = 3,
}
