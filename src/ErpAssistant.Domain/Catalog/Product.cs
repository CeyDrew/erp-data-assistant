using ErpAssistant.Domain.Common;

namespace ErpAssistant.Domain.Catalog;

public sealed class Product : Entity
{
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public int CategoryId { get; set; }
    public ProductCategory Category { get; set; } = null!;
    public required string Unit { get; set; }

    /// <summary>EAN-13 barkod (mobilde kamerayla ürün arama).</summary>
    public string? Barcode { get; set; }

    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public int ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;
}
