using ErpAssistant.Domain.Billing;
using ErpAssistant.Domain.Catalog;
using ErpAssistant.Domain.Common;
using ErpAssistant.Domain.Sales;
using FluentAssertions;

namespace ErpAssistant.UnitTests.Domain;

public class InvoiceTests
{
    private static Order ConfirmedOrder(decimal amount)
    {
        var customer = new Customer { Id = 1, Code = "M", Name = "M", TaxNumber = "1", City = "X", SalesRepresentativeId = 2 };
        var order = Order.Create("SP-1", customer, 1, new DateTime(2026, 3, 1));
        order.AddLine(new Product { Id = 1, Sku = "S", Name = "P", Unit = "adet", UnitPrice = amount }, 1);
        order.Confirm();
        return order;
    }

    [Fact]
    public void ForOrder_calculates_vat_and_due_date()
    {
        var invoice = Invoice.ForOrder("FT-1", ConfirmedOrder(1000m), new DateTime(2026, 3, 2), 30);

        invoice.NetAmount.Should().Be(1000m);
        invoice.TaxAmount.Should().Be(200m);
        invoice.TotalAmount.Should().Be(1200m);
        invoice.DueDate.Should().Be(new DateTime(2026, 4, 1));
        invoice.SalesRepresentativeId.Should().Be(2);
    }

    [Fact]
    public void Draft_order_cannot_be_invoiced()
    {
        var customer = new Customer { Id = 1, Code = "M", Name = "M", TaxNumber = "1", City = "X" };
        var draft = Order.Create("SP-1", customer, 1, new DateTime(2026, 3, 1));

        var act = () => Invoice.ForOrder("FT-1", draft, new DateTime(2026, 3, 2), 30);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Payments_move_status_to_partially_paid_then_paid()
    {
        var invoice = Invoice.ForOrder("FT-1", ConfirmedOrder(1000m), new DateTime(2026, 3, 2), 30);

        invoice.RegisterPayment(500m, new DateTime(2026, 3, 10), PaymentMethod.BankTransfer);
        invoice.Status.Should().Be(InvoiceStatus.PartiallyPaid);
        invoice.Outstanding.Should().Be(700m);

        invoice.RegisterPayment(700m, new DateTime(2026, 3, 20), PaymentMethod.BankTransfer);
        invoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public void Overpayment_is_rejected()
    {
        var invoice = Invoice.ForOrder("FT-1", ConfirmedOrder(100m), new DateTime(2026, 3, 2), 30);

        invoice.Invoking(i => i.RegisterPayment(121m, DateTime.Today, PaymentMethod.Cash))
            .Should().Throw<DomainException>();
    }
}
