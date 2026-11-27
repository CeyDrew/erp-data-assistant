using ErpAssistant.Application.Field;
using ErpAssistant.Domain.Catalog;
using ErpAssistant.Domain.Common;
using ErpAssistant.Domain.Field;
using ErpAssistant.Domain.Sales;
using FluentAssertions;

namespace ErpAssistant.UnitTests.Domain;

public class VisitTests
{
    private static readonly DateTime Morning = new(2026, 11, 27, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Plan_check_in_and_check_out_completes_the_visit()
    {
        var visit = Visit.Plan(3, 12, new DateOnly(2026, 11, 27), 1);

        visit.CheckIn(Morning, 41.0, 29.0);
        visit.CheckOut(Morning.AddMinutes(25), "  Yeni katalog bırakıldı ");

        visit.Status.Should().Be(VisitStatus.Completed);
        visit.CheckInLatitude.Should().Be(41.0);
        visit.CheckOutAt.Should().Be(Morning.AddMinutes(25));
        visit.Note.Should().Be("Yeni katalog bırakıldı");
    }

    [Fact]
    public void Check_out_requires_check_in()
    {
        var visit = Visit.Plan(3, 12, new DateOnly(2026, 11, 27), 1);

        visit.Invoking(v => v.CheckOut(Morning, null)).Should().Throw<DomainException>();
    }

    [Fact]
    public void Double_check_in_is_rejected()
    {
        var visit = Visit.Plan(3, 12, new DateOnly(2026, 11, 27), 1);
        visit.CheckIn(Morning, null, null);

        visit.Invoking(v => v.CheckIn(Morning, null, null)).Should().Throw<DomainException>();
    }

    [Fact]
    public void Check_out_cannot_precede_check_in()
    {
        var visit = Visit.Plan(3, 12, new DateOnly(2026, 11, 27), 1);
        visit.CheckIn(Morning, null, null);

        visit.Invoking(v => v.CheckOut(Morning.AddMinutes(-1), null)).Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(41.0, null)]
    [InlineData(91.0, 29.0)]
    [InlineData(41.0, 181.0)]
    public void Check_in_rejects_invalid_location(double? latitude, double? longitude)
    {
        var visit = Visit.Plan(3, 12, new DateOnly(2026, 11, 27), 1);

        visit.Invoking(v => v.CheckIn(Morning, latitude, longitude)).Should().Throw<DomainException>();
    }

    [Fact]
    public void Only_planned_visit_can_be_skipped()
    {
        var visit = Visit.Plan(3, 12, new DateOnly(2026, 11, 27), 1);
        visit.Skip("Müşteri kapalı");
        visit.Status.Should().Be(VisitStatus.Skipped);

        visit.Invoking(v => v.CheckIn(Morning, null, null)).Should().Throw<DomainException>();
    }
}

public class DeliveryTests
{
    private static readonly DateTime OrderDate = new(2026, 11, 25, 10, 0, 0, DateTimeKind.Utc);

    private static Order ShippedOrder()
    {
        var customer = new Customer { Id = 7, Code = "M-0007", Name = "Test A.Ş.", TaxNumber = "1", City = "İzmir", SalesRepresentativeId = 3 };
        var order = Order.Create("SP-1", customer, 1, OrderDate);
        order.Id = 901;
        order.AddLine(new Product { Id = 1, Sku = "SKU-1", Name = "Rulman", Unit = "adet", UnitPrice = 10m }, 2);
        order.Confirm();
        order.Ship(OrderDate.AddDays(1));
        return order;
    }

    [Fact]
    public void Assign_requires_shipped_order_and_copies_sales_rep()
    {
        var order = ShippedOrder();
        var delivery = Delivery.Assign(order, 2, new DateOnly(2026, 11, 27), 1);

        delivery.SalesRepresentativeId.Should().Be(3);
        delivery.Status.Should().Be(DeliveryStatus.Assigned);

        var draft = Order.Create("SP-2", new Customer { Code = "x", Name = "x", TaxNumber = "2", City = "x" }, 1, OrderDate);
        FluentActions.Invoking(() => Delivery.Assign(draft, 2, new DateOnly(2026, 11, 27), 1)).Should().Throw<DomainException>();
    }

    [Fact]
    public void Complete_marks_order_delivered()
    {
        var order = ShippedOrder();
        var delivery = Delivery.Assign(order, 2, new DateOnly(2026, 11, 27), 1);

        delivery.Complete(order, OrderDate.AddDays(2), " Ali Veli ", null, 38.4, 27.1);

        delivery.Status.Should().Be(DeliveryStatus.Delivered);
        delivery.RecipientName.Should().Be("Ali Veli");
        order.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public void Complete_requires_recipient_and_assigned_state()
    {
        var order = ShippedOrder();
        var delivery = Delivery.Assign(order, 2, new DateOnly(2026, 11, 27), 1);

        delivery.Invoking(d => d.Complete(order, OrderDate, " ", null, null, null)).Should().Throw<DomainException>();
        order.Status.Should().Be(OrderStatus.Shipped);

        delivery.Fail(OrderDate.AddDays(2), DeliveryFailureReason.CustomerAbsent, null);
        delivery.Invoking(d => d.Complete(order, OrderDate, "Ali", null, null, null)).Should().Throw<DomainException>();
    }

    [Fact]
    public void Fail_with_other_reason_requires_note()
    {
        var delivery = Delivery.Assign(ShippedOrder(), 2, new DateOnly(2026, 11, 27), 1);

        delivery.Invoking(d => d.Fail(OrderDate, DeliveryFailureReason.Other, null)).Should().Throw<DomainException>();

        delivery.Fail(OrderDate, DeliveryFailureReason.Other, "Rampa kapalı");
        delivery.Status.Should().Be(DeliveryStatus.Failed);
        delivery.FailureReason.Should().Be(DeliveryFailureReason.Other);
    }

    [Fact]
    public void Failed_delivery_cannot_take_proof()
    {
        var delivery = Delivery.Assign(ShippedOrder(), 2, new DateOnly(2026, 11, 27), 1);
        delivery.Fail(OrderDate, DeliveryFailureReason.Refused, null);

        delivery.Invoking(d => d.AttachProof("a.jpg", "b.png")).Should().Throw<DomainException>();
    }

    [Fact]
    public void Order_line_shortage_is_bounded_by_quantity()
    {
        var line = new OrderLine { Quantity = 5 };
        line.FlagShortage(5);
        line.ShortQuantity.Should().Be(5);

        line.Invoking(l => l.FlagShortage(6)).Should().Throw<DomainException>();
        line.Invoking(l => l.FlagShortage(-1)).Should().Throw<DomainException>();
    }
}

public class FieldSupportTests
{
    [Fact]
    public void Ean13_barcodes_are_valid_and_unique_per_product()
    {
        var codes = Enumerable.Range(1, 500).Select(Ean13.ForProduct).ToList();

        codes.Should().OnlyHaveUniqueItems();
        codes.Should().OnlyContain(c => Ean13.IsValid(c) && c.StartsWith("869"));
        Ean13.IsValid("8690000000034").Should().BeFalse(); // örnek sözleşmedeki rastgele değer
    }

    [Theory]
    [InlineData(null, 0UL)]
    [InlineData("", 0UL)]
    [InlineData("0", 0UL)]
    [InlineData("00000000000007D3", 2003UL)]
    public void Cursor_round_trips_as_16_hex_digits(string? cursor, ulong expected)
    {
        FieldCursor.Parse(cursor).Should().Be(expected);
        FieldCursor.Format(expected).Should().HaveLength(16);
    }

    [Fact]
    public void Invalid_cursor_is_a_validation_error()
    {
        FluentActions.Invoking(() => FieldCursor.Parse("xyz")).Should().Throw<FluentValidation.ValidationException>();
    }

    [Fact]
    public void Order_hash_ignores_decimal_scale_but_not_values()
    {
        var a = new FieldOrderRequest(1, 1, null, [new FieldOrderLineRequest(3, 10, 129.90m, 0m)]);
        var b = new FieldOrderRequest(1, 1, null, [new FieldOrderLineRequest(3, 10, 129.9m, 0.00m)]);
        var c = a with { Lines = [new FieldOrderLineRequest(3, 11, 129.90m, 0m)] };

        FieldJson.Hash(a).Should().Be(FieldJson.Hash(b));
        FieldJson.Hash(a).Should().NotBe(FieldJson.Hash(c));
    }

    [Fact]
    public void Known_cities_resolve_including_bogus_spellings()
    {
        TurkishCities.TryGetCenter("İSTANBUL", out _).Should().BeTrue();
        TurkishCities.TryGetCenter("K.maraş", out _).Should().BeTrue();
        TurkishCities.TryGetCenter("İçel (Mersin)", out _).Should().BeTrue();
        TurkishCities.DistanceKm(41.0082, 28.9784, 39.9334, 32.8597).Should().BeInRange(340, 360);
    }
}
