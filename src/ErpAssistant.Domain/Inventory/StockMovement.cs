using ErpAssistant.Domain.Catalog;
using ErpAssistant.Domain.Common;

namespace ErpAssistant.Domain.Inventory;

/// <summary>
/// Stok defterinin tek satırı. Bakiye saklanmaz; hareketlerin toplamından hesaplanır
/// (veritabanında <c>vw_StockBalance</c> indekslenmiş view'ı).
/// </summary>
public sealed class StockMovement : Entity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public StockMovementType Type { get; set; }

    /// <summary>İşaretli miktar: giriş pozitif, çıkış negatif.</summary>
    public int Quantity { get; set; }

    public DateTime OccurredAt { get; set; }
    public string? Reference { get; set; }

    public static StockMovement Create(int productId, int warehouseId, StockMovementType type, int quantity,
        DateTime occurredAt, string? reference = null)
    {
        if (quantity == 0)
            throw new DomainException("Stok hareketi miktarı sıfır olamaz.");

        var outgoing = type is StockMovementType.Sale or StockMovementType.TransferOut;
        var incoming = type is StockMovementType.Purchase or StockMovementType.TransferIn or StockMovementType.Return;
        if (outgoing) quantity = -Math.Abs(quantity);
        if (incoming) quantity = Math.Abs(quantity);

        return new StockMovement
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            Type = type,
            Quantity = quantity,
            OccurredAt = occurredAt,
            Reference = reference,
        };
    }
}
