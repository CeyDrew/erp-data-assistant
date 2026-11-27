using ErpAssistant.Domain.Billing;
using ErpAssistant.Domain.Catalog;
using ErpAssistant.Domain.Common;
using ErpAssistant.Domain.Inventory;
using ErpAssistant.Domain.Organization;

namespace ErpAssistant.Domain.Sales;

public sealed class Order : Entity
{
    public required string OrderNumber { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    /// <summary>
    /// Siparişin temsilcisi. Müşteriden kopyalanır (denormalize): satır yetkisi filtresi ve
    /// temsilci bazlı raporlar join gerektirmeden index üzerinden çalışır.
    /// </summary>
    public int SalesRepresentativeId { get; set; }
    public SalesRepresentative SalesRepresentative { get; set; } = null!;

    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime? ShippedAt { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>Mobil istemcinin idempotency anahtarı; aynı istek iki kez sipariş oluşturamaz.</summary>
    public Guid? ClientRequestId { get; set; }

    public string? Note { get; set; }

    public List<OrderLine> Lines { get; set; } = [];
    public Invoice? Invoice { get; set; }

    public static Order Create(string orderNumber, Customer customer, int warehouseId, DateTime orderDate) => new()
    {
        OrderNumber = orderNumber,
        CustomerId = customer.Id,
        Customer = customer,
        SalesRepresentativeId = customer.SalesRepresentativeId,
        WarehouseId = warehouseId,
        OrderDate = orderDate,
        Status = OrderStatus.Draft,
    };

    public OrderLine AddLine(Product product, int quantity, decimal discountRate = 0)
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException("Yalnızca taslak siparişe kalem eklenebilir.");
        if (quantity <= 0)
            throw new DomainException("Kalem miktarı pozitif olmalı.");
        if (discountRate is < 0 or > 0.5m)
            throw new DomainException("İskonto oranı %0 ile %50 arasında olmalı.");

        var line = new OrderLine
        {
            ProductId = product.Id,
            Product = product,
            Quantity = quantity,
            UnitPrice = product.UnitPrice,
            DiscountRate = discountRate,
            LineTotal = OrderLine.CalculateTotal(quantity, product.UnitPrice, discountRate),
        };
        Lines.Add(line);
        TotalAmount = Lines.Sum(l => l.LineTotal);
        return line;
    }

    public void Confirm()
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException("Yalnızca taslak sipariş onaylanabilir.");
        if (Lines.Count == 0)
            throw new DomainException("Kalemsiz sipariş onaylanamaz.");
        Status = OrderStatus.Confirmed;
    }

    public void Ship(DateTime shippedAt)
    {
        if (Status != OrderStatus.Confirmed)
            throw new DomainException("Yalnızca onaylı sipariş sevk edilebilir.");
        if (shippedAt < OrderDate)
            throw new DomainException("Sevk tarihi sipariş tarihinden önce olamaz.");
        Status = OrderStatus.Shipped;
        ShippedAt = shippedAt;
    }

    public void MarkDelivered()
    {
        if (Status != OrderStatus.Shipped)
            throw new DomainException("Yalnızca sevk edilmiş sipariş teslim edilebilir.");
        Status = OrderStatus.Delivered;
    }

    public void Cancel()
    {
        if (Status is OrderStatus.Shipped or OrderStatus.Delivered)
            throw new DomainException("Sevk edilmiş sipariş iptal edilemez.");
        Status = OrderStatus.Cancelled;
    }

    public static readonly OrderStatus[] OpenStatuses = [OrderStatus.Draft, OrderStatus.Confirmed];
}
