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

    public static decimal CalculateTotal(int quantity, decimal unitPrice, decimal discountRate) =>
        Math.Round(quantity * unitPrice * (1 - discountRate), 2, MidpointRounding.AwayFromZero);
}
