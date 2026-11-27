using ErpAssistant.Application.Abstractions;
using ErpAssistant.Domain.Billing;
using ErpAssistant.Domain.Catalog;
using ErpAssistant.Domain.Field;
using ErpAssistant.Domain.Inventory;
using ErpAssistant.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpAssistant.Application.Field;

/// <summary>
/// Demo planlayıcı: mobil uygulama her gün dolu bir planla açılsın diye, temsilci ya da sürücü senkron
/// yaptığında bugün için kaydı yoksa deterministik bir plan oluşturur. Gerçek bir kurulumda bu planı
/// rota/dağıtım planlama sistemi yazar; arayüz (Visits/Deliveries tabloları) aynı kalır.
/// </summary>
public sealed class FieldDemoPlanner(
    IErpDbContext db,
    IFieldDatabase fieldDb,
    ICurrentUser user,
    TimeProvider clock,
    ILogger<FieldDemoPlanner> logger)
{
    public const int VisitsPerDay = 6;
    public const int DeliveriesPerDay = 8;
    public const double NearbyKm = 150;
    public const int PaymentTermDays = 30;

    public async Task EnsureTodayAsync(CancellationToken ct)
    {
        var today = FieldClock.Today(clock);
        if (user.Roles.Contains(Roles.SalesRep) && user.SalesRepresentativeId is { } repId)
            await RunAsync(() => PlanVisitsAsync(repId, today, ct), ct);
        if (user.Roles.Contains(Roles.Driver) && user.DriverId is { } driverId)
            await RunAsync(() => PlanDeliveriesAsync(driverId, today, ct), ct);
    }

    private async Task RunAsync(Func<Task> plan, CancellationToken ct)
    {
        try
        {
            await plan();
        }
        catch (DbUpdateException ex) when (fieldDb.IsUniqueViolation(ex))
        {
            // Aynı kullanıcının eşzamanlı iki senkronu: (kişi, gün, sıra) benzersiz index'i ikinci planı reddeder.
            fieldDb.DiscardChanges();
            logger.LogInformation("Günlük plan eşzamanlı olarak oluşturuldu; ikinci deneme atlandı");
        }
    }

    private async Task PlanVisitsAsync(int repId, DateOnly today, CancellationToken ct)
    {
        if (await db.Visits.AnyAsync(v => v.SalesRepresentativeId == repId && v.PlannedDate == today, ct))
            return;

        // Satır filtresi: yalnızca temsilcinin kendi müşterileri.
        var customers = await db.Customers.AsNoTracking()
            .Where(c => c.SalesRepresentativeId == repId)
            .OrderBy(c => c.Id)
            .Select(c => new Stop(c.Id, c.Latitude, c.Longitude))
            .ToListAsync(ct);
        if (customers.Count == 0) return;

        // Her gün farklı bir dilim: günün numarasına göre kaydırılmış pencere, sonra en yakın komşu rotası.
        var count = Math.Min(VisitsPerDay, customers.Count);
        var start = today.DayNumber * VisitsPerDay % customers.Count;
        var picked = Enumerable.Range(0, count).Select(i => customers[(start + i) % customers.Count]).ToList();
        var route = Route(picked, picked[0].Latitude ?? TurkishCities.Default.Latitude, picked[0].Longitude ?? TurkishCities.Default.Longitude);

        for (var i = 0; i < route.Count; i++)
            db.Visits.Add(Visit.Plan(repId, route[i].Id, today, i + 1));
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Temsilci {RepId} için {Count} ziyaret planlandı ({Date})", repId, route.Count, today);
    }

    /// <remarks>
    /// Planlayıcı sunucu içi bir iş: sürücünün sipariş ve müşteri satırlarına yetkisi olmadığından okumalar
    /// <c>IgnoreQueryFilters()</c> ile yapılır. Sürücüye buradan veri dönmez; yalnızca teslimat kaydı yazılır
    /// ve sürücü onu teslimat filtresinden geçerek görür.
    /// </remarks>
    private async Task PlanDeliveriesAsync(int driverId, DateOnly today, CancellationToken ct)
    {
        if (await db.Deliveries.IgnoreQueryFilters().AnyAsync(d => d.DriverId == driverId && d.PlannedDate == today, ct))
            return;

        var driver = await db.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.Id == driverId, ct);
        if (driver is null) return;
        var (lat, lng) = TurkishCities.CenterOf(driver.City);

        // Henüz teslimata atanmamış onaylı siparişler; sürücünün şehrine yakın olanlar önce.
        var candidates = await db.Orders.IgnoreQueryFilters().AsNoTracking()
            .Where(o => o.Status == OrderStatus.Confirmed && !db.Deliveries.IgnoreQueryFilters().Any(d => d.OrderId == o.Id))
            .Select(o => new Stop(o.Id, o.Customer.Latitude, o.Customer.Longitude))
            .ToListAsync(ct);
        var nearby = candidates
            .Where(s => s.Latitude is not null && Distance(s, lat, lng) <= NearbyKm)
            .OrderBy(s => Distance(s, lat, lng)).ThenBy(s => s.Id)
            .Take(DeliveriesPerDay)
            .Select(s => s.Id)
            .ToList();

        var orders = await db.Orders.IgnoreQueryFilters()
            .Include(o => o.Lines)
            .Where(o => nearby.Contains(o.Id))
            .ToListAsync(ct);

        // Yakında yeterli onaylı sipariş yoksa (demo verisi günler içinde tükenir) şehre en yakın
        // müşteriler için onaylı demo siparişleri açılır.
        if (orders.Count < DeliveriesPerDay)
            orders.AddRange(await CreateDemoOrdersAsync(driver.City, lat, lng, DeliveriesPerDay - orders.Count, today, ct));
        if (orders.Count == 0) return;

        var now = clock.GetUtcNow().UtcDateTime;
        var customerIds = orders.Select(o => o.CustomerId).Distinct().ToList();
        var customerPositions = await db.Customers.IgnoreQueryFilters().AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new Stop(c.Id, c.Latitude, c.Longitude), ct);
        var route = Route(orders.Select((o, index) => customerPositions[o.CustomerId] with { Id = index }).ToList(), lat, lng);

        for (var i = 0; i < route.Count; i++)
        {
            var order = orders[route[i].Id];
            // Seeder'daki gibi: sevk stok çıkışı yazar, fatura kesilir, sipariş Shipped olur.
            order.Ship(now < order.OrderDate ? order.OrderDate : now);
            foreach (var line in order.Lines)
            {
                db.StockMovements.Add(StockMovement.Create(line.ProductId, order.WarehouseId, StockMovementType.Sale,
                    line.Quantity, order.ShippedAt!.Value, order.OrderNumber));
            }
            var invoiceSequence = await fieldDb.NextInvoiceSequenceAsync(ct);
            order.Invoice = Invoice.ForOrder($"FS-{now:yyyy}-{invoiceSequence:D6}", order, order.ShippedAt!.Value.Date, PaymentTermDays);
            db.Deliveries.Add(Delivery.Assign(order, driverId, today, i + 1));
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Sürücü {DriverId} için {Count} teslimat planlandı ({Date})", driverId, route.Count, today);
    }

    private async Task<List<Order>> CreateDemoOrdersAsync(string city, double lat, double lng, int count, DateOnly today,
        CancellationToken ct)
    {
        var customers = (await db.Customers.IgnoreQueryFilters()
                .Where(c => c.Latitude != null && c.Longitude != null)
                .ToListAsync(ct))
            .OrderBy(c => TurkishCities.DistanceKm(lat, lng, c.Latitude!.Value, c.Longitude!.Value)).ThenBy(c => c.Id)
            .Take(Math.Max(count, 1) * 2)
            .ToList();
        var products = await db.Products
            .Where(p => p.IsActive && p.UnitPrice < 1_000)
            .OrderBy(p => p.Id)
            .Take(40)
            .ToListAsync(ct);
        var warehouse = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.City == city, ct)
            ?? await db.Warehouses.AsNoTracking().OrderBy(w => w.Id).FirstOrDefaultAsync(ct);
        if (customers.Count == 0 || products.Count == 0 || warehouse is null) return [];

        var now = clock.GetUtcNow().UtcDateTime;
        var orders = new List<Order>();
        for (var i = 0; i < count; i++)
        {
            var customer = customers[(today.DayNumber + i) % customers.Count];
            var sequence = await fieldDb.NextOrderSequenceAsync(ct);
            var order = Order.Create($"SM-{now:yyyy}-{sequence:D6}", customer, warehouse.Id, now.AddHours(-2));
            order.Note = "Demo planlayıcı";
            var lineCount = 1 + (today.DayNumber + i) % 3;
            for (var k = 0; k < lineCount; k++)
            {
                Product product = products[(today.DayNumber * 7 + i * 3 + k) % products.Count];
                if (order.Lines.Any(l => l.ProductId == product.Id)) continue;
                order.AddLine(product, 2 + (i + k) % 5 * (product.Unit == "metre" ? 10 : 1));
            }
            order.Confirm();
            db.Orders.Add(order);
            orders.Add(order);
        }
        return orders;
    }

    private static double Distance(Stop s, double lat, double lng) =>
        s.Latitude is { } a && s.Longitude is { } b ? TurkishCities.DistanceKm(lat, lng, a, b) : double.MaxValue;

    /// <summary>En yakın komşu sıralaması: başlangıçtan en yakın durağa, oradan kalanların en yakınına.</summary>
    private static List<Stop> Route(List<Stop> stops, double lat, double lng)
    {
        var remaining = stops.ToList();
        var route = new List<Stop>(stops.Count);
        while (remaining.Count > 0)
        {
            var next = remaining.OrderBy(s => Distance(s, lat, lng)).ThenBy(s => s.Id).First();
            remaining.Remove(next);
            route.Add(next);
            if (next.Latitude is { } a && next.Longitude is { } b) (lat, lng) = (a, b);
        }
        return route;
    }

    private sealed record Stop(int Id, double? Latitude, double? Longitude);
}
