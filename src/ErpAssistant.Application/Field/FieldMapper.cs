using ErpAssistant.Application.Abstractions;
using ErpAssistant.Domain.Field;
using ErpAssistant.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace ErpAssistant.Application.Field;

internal static class FieldMapper
{
    public static VisitSync ToSync(Visit v) => new(
        v.Id, v.CustomerId, v.PlannedDate, v.Sequence, v.Status, FieldClock.Utc(v.CheckInAt), FieldClock.Utc(v.CheckOutAt), v.Note);

    /// <summary>Bellekteki (yeni oluşturulmuş) sipariş; kalemlerin ürünleri yüklü olmalı.</summary>
    public static OrderSync ToSync(Order o) => new(
        o.Id, o.OrderNumber, o.ClientRequestId, o.CustomerId, o.WarehouseId, FieldClock.Utc(o.OrderDate), o.Status, o.TotalAmount,
        o.Lines.OrderBy(l => l.Id).Select(l => new OrderLineSync(
            l.ProductId, l.Product.Name, l.Quantity, l.UnitPrice, l.DiscountRate, l.LineTotal, l.ShortQuantity)).ToList());

    /// <summary>
    /// Teslimatları müşteri adı/adres/konum ve kalemlerle zenginleştirir.
    /// </summary>
    /// <remarks>
    /// Sürücünün müşteri ve sipariş satırlarına satır yetkisi yoktur (<see cref="AccessPolicy.RowFilterFor"/>
    /// NoAccess döner), bu yüzden sipariş/müşteri okuması <c>IgnoreQueryFilters()</c> ile yapılır. Bu güvenlidir:
    /// sorgu yalnızca <paramref name="deliveries"/> listesindeki sipariş kimlikleriyle sınırlıdır ve bu liste
    /// teslimat filtresinden (sürücü yalnızca kendi teslimatları) geçmiş satırlardan gelir. Sonuçtan da
    /// yalnızca teslimat için gereken alanlar (ad, adres, telefon, konum, kalem adı/miktarı) projekte edilir;
    /// fiyat, bakiye ve diğer siparişler sürücüye hiç ulaşmaz.
    /// </remarks>
    public static async Task<List<DeliverySync>> EnrichAsync(IErpDbContext db, IReadOnlyList<Delivery> deliveries, CancellationToken ct)
    {
        if (deliveries.Count == 0) return [];

        var orderIds = deliveries.Select(d => d.OrderId).Distinct().ToList();
        var orders = await db.Orders.IgnoreQueryFilters().AsNoTracking()
            .Where(o => orderIds.Contains(o.Id))
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.Customer.Name,
                o.Customer.Address,
                o.Customer.City,
                o.Customer.Phone,
                o.Customer.Latitude,
                o.Customer.Longitude,
                Items = o.Lines.OrderBy(l => l.Id)
                    .Select(l => new DeliveryItemSync(l.Product.Name, l.Quantity, l.Product.Unit)).ToList(),
            })
            .ToDictionaryAsync(o => o.Id, ct);

        return deliveries.Where(d => orders.ContainsKey(d.OrderId)).Select(d =>
        {
            var o = orders[d.OrderId];
            return new DeliverySync(d.Id, d.OrderId, o.OrderNumber, d.PlannedDate, d.Sequence, d.Status, o.Name, o.Address,
                o.City, o.Phone, o.Latitude, o.Longitude, o.Items, FieldClock.Utc(d.DeliveredAt), d.FailureReason,
                d.RecipientName, d.Note, d.HasProof);
        }).ToList();
    }
}
