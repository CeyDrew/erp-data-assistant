using ErpAssistant.Domain.Catalog;
using ErpAssistant.Domain.Common;
using ErpAssistant.Domain.Sales;
using FluentAssertions;

namespace ErpAssistant.UnitTests.Domain;

public class OrderTests
{
    private static readonly Customer Customer = new()
    {
        Id = 7, Code = "M-0007", Name = "Test A.Ş.", TaxNumber = "1234567890", City = "Bursa", SalesRepresentativeId = 3,
    };

    private static Product Product(decimal price) => new()
    {
        Id = 1, Sku = "SKU-1", Name = "Rulman", Unit = "adet", UnitPrice = price,
    };

    [Fact]
    public void Create_copies_sales_representative_from_customer()
    {
        var order = Order.Create("SP-1", Customer, 1, new DateTime(2026, 1, 10));

        order.SalesRepresentativeId.Should().Be(3);
        order.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public void AddLine_applies_discount_and_updates_total()
    {
        var order = Order.Create("SP-1", Customer, 1, new DateTime(2026, 1, 10));

        order.AddLine(Product(100m), 3, 0.10m);
        order.AddLine(Product(19.99m), 1);

        order.Lines[0].LineTotal.Should().Be(270m);
        order.TotalAmount.Should().Be(289.99m);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(1, 0.6)]
    public void AddLine_rejects_invalid_values(int quantity, double discount)
    {
        var order = Order.Create("SP-1", Customer, 1, new DateTime(2026, 1, 10));

        var act = () => order.AddLine(Product(10m), quantity, (decimal)discount);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Lifecycle_follows_draft_confirmed_shipped_delivered()
    {
        var order = Order.Create("SP-1", Customer, 1, new DateTime(2026, 1, 10));
        order.AddLine(Product(10m), 1);

        order.Confirm();
        order.Ship(new DateTime(2026, 1, 12));
        order.MarkDelivered();

        order.Status.Should().Be(OrderStatus.Delivered);
        order.ShippedAt.Should().Be(new DateTime(2026, 1, 12));
    }

    [Fact]
    public void Empty_order_cannot_be_confirmed()
    {
        var order = Order.Create("SP-1", Customer, 1, new DateTime(2026, 1, 10));

        order.Invoking(o => o.Confirm()).Should().Throw<DomainException>();
    }

    [Fact]
    public void Shipped_order_cannot_be_cancelled()
    {
        var order = Order.Create("SP-1", Customer, 1, new DateTime(2026, 1, 10));
        order.AddLine(Product(10m), 1);
        order.Confirm();
        order.Ship(new DateTime(2026, 1, 11));

        order.Invoking(o => o.Cancel()).Should().Throw<DomainException>();
    }

    [Fact]
    public void Ship_date_cannot_precede_order_date()
    {
        var order = Order.Create("SP-1", Customer, 1, new DateTime(2026, 1, 10));
        order.AddLine(Product(10m), 1);
        order.Confirm();

        order.Invoking(o => o.Ship(new DateTime(2026, 1, 9))).Should().Throw<DomainException>();
    }
}
