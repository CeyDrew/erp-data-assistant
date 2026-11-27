using ErpAssistant.Domain.Catalog;
using ErpAssistant.Domain.Common;

namespace ErpAssistant.Domain.Sales;

public sealed class OrderLine : Entity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>0 ile 1 arası iskonto oranı.</summary>
    public decimal DiscountRate { get; set; }

    public decimal LineTotal { get; set; }

    /// <summary>Sipariş anında depoda karşılanamayan miktar. Kalem düşürülmez; eksik olarak işaretlenir.</summary>
    public int ShortQuantity { get; set; }

    public void FlagShortage(int shortQuantity)
    {
        if (shortQuantity < 0 || shortQuantity > Quantity)
            throw new DomainException("Eksik miktar 0 ile kalem miktarı arasında olmalı.");
        ShortQuantity = shortQuantity;
    }

    public static decimal CalculateTotal(int quantity, decimal unitPrice, decimal discountRate) =>
        Math.Round(quantity * unitPrice * (1 - discountRate), 2, MidpointRounding.AwayFromZero);
}
