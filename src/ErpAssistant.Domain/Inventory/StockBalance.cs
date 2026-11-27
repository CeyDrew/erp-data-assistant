namespace ErpAssistant.Domain.Inventory;

/// <summary>Salt okunur projeksiyon: ürün × depo bazında güncel bakiye (indekslenmiş view).</summary>
public sealed class StockBalance
{
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public int Quantity { get; set; }
    public long MovementCount { get; set; }
}
