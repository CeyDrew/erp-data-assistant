using ErpAssistant.Application.Abstractions;
using ErpAssistant.Domain.Billing;
using ErpAssistant.Domain.Field;
using ErpAssistant.Domain.Sales;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ErpAssistant.Application.Field;

/// <summary>
/// Mobil delta senkron. Her varlık türü kendi rowversion'ına göre <c>(since, upper]</c> aralığında okunur;
/// <c>upper</c> = <c>MIN_ACTIVE_ROWVERSION() - 1</c> (bkz. <see cref="IFieldDatabase.SyncUpperBoundAsync"/>).
/// Satır yetkisi global query filter'lardan gelir; burada ek filtre yalnızca tarih penceresi ve rol profilidir.
/// </summary>
public sealed class FieldSyncService(
    IErpDbContext db,
    IFieldDatabase fieldDb,
    ICurrentUser user,
    FieldDemoPlanner planner,
    TimeProvider clock)
{
    public const int DefaultLimit = 500;
    public const int MaxLimit = 1000;
    public const int OrderHistoryDays = 90;
    public const int PlanHistoryDays = 7;

    private const string V = FieldVersion.Property;

    public async Task<SyncChangesResponse> ChangesAsync(string? since, int? limit, CancellationToken ct)
    {
        var from = FieldCursor.Parse(since);
        var take = limit ?? DefaultLimit;
        if (take is < 1 or > MaxLimit)
            throw new ValidationException([new ValidationFailure("limit", $"limit 1 ile {MaxLimit} arasında olmalı.")]);

        // Planlayıcı üst sınır okunmadan önce çalışır; bugün için oluşturduğu kayıtlar bu yanıta girer.
        await planner.EnsureTodayAsync(ct);

        var upper = await fieldDb.SyncUpperBoundAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var isAdmin = user.Roles.Contains(Roles.Admin);
        var sales = isAdmin || user.Roles.Contains(Roles.SalesRep);
        var driver = isAdmin || user.Roles.Contains(Roles.Driver);

        var warehouses = await db.Warehouses.AsNoTracking().OrderBy(w => w.Id)
            .Select(w => new WarehouseSync(w.Id, w.Code, w.Name, w.City))
            .ToListAsync(ct);

        if (upper <= from)
        {
            // Son senkrondan beri commit edilmiş değişiklik yok.
            return new SyncChangesResponse(FieldCursor.Format(from), false, now, warehouses, [], [], [], [], [], [], []);
        }

        var page = new Pager(take);
        var today = FieldClock.Today(clock);

        var customers = sales ? await CustomersAsync(from, upper, page, ct) : [];
        var products = sales ? await ProductsAsync(from, upper, page, ct) : [];
        var stock = sales ? await StockAsync(from, upper, page, ct) : [];
        var orders = sales ? await OrdersAsync(from, upper, now.AddDays(-OrderHistoryDays), page, ct) : [];
        var visits = sales ? await VisitsAsync(from, upper, today.AddDays(-PlanHistoryDays), page, ct) : [];
        var deliveries = driver ? await DeliveriesAsync(from, upper, today.AddDays(-PlanHistoryDays), page, ct) : [];
        var tombstones = from == 0 ? [] : await TombstonesAsync(from, upper, page, ct);

        // Aynı pencerede hem silinip hem geri gelen (ör. temsilciye geri atanan müşteri) satırın
        // tombstone'u düşürülür; istemci uygulama sırasından bağımsız olarak doğru sonuca ulaşır.
        var present = new HashSet<(string, int)>(
            customers.Select(c => (FieldTombstone.Customer, c.Id))
                .Concat(products.Select(p => (FieldTombstone.Product, p.Id)))
                .Concat(visits.Select(v => (FieldTombstone.Visit, v.Id)))
                .Concat(deliveries.Select(d => (FieldTombstone.Delivery, d.Id))));
        tombstones = tombstones.Where(t => !present.Contains((t.Entity, t.Id))).Distinct().ToList();

        return new SyncChangesResponse(
            FieldCursor.Format(page.Cursor(upper)), page.HasMore, now, warehouses,
            customers, products, stock, orders, visits, deliveries, tombstones);
    }

    /// <summary>
    /// Müşteri, kendi satırı ya da faturası/tahsilatı değiştiyse gelir (bakiye değişir). Sayfalama için
    /// müşterinin "etkin sürümü" bu üç kaynaktaki en büyük rowversion'dır.
    /// </summary>
    private async Task<List<CustomerSync>> CustomersAsync(ulong from, ulong upper, Pager page, CancellationToken ct)
    {
        var own = await db.Customers.AsNoTracking()
            .Where(c => EF.Property<ulong>(c, V) > from && EF.Property<ulong>(c, V) <= upper)
            .Select(c => new Changed(c.Id, EF.Property<ulong>(c, V)))
            .ToListAsync(ct);
        var invoices = await db.Invoices.AsNoTracking()
            .Where(i => EF.Property<ulong>(i, V) > from && EF.Property<ulong>(i, V) <= upper)
            .Select(i => new Changed(i.CustomerId, EF.Property<ulong>(i, V)))
            .ToListAsync(ct);
        var payments = await db.Payments.AsNoTracking()
            .Where(p => EF.Property<ulong>(p, V) > from && EF.Property<ulong>(p, V) <= upper)
            .Select(p => new Changed(p.Invoice.CustomerId, EF.Property<ulong>(p, V)))
            .ToListAsync(ct);

        var candidates = own.Concat(invoices).Concat(payments)
            .GroupBy(x => x.Id)
            .Select(g => new Changed(g.Key, g.Max(x => x.Version)))
            .OrderBy(x => x.Version).ThenBy(x => x.Id)
            .ToList();
        var selected = page.Take(FieldTombstone.Customer, candidates, x => x.Version);
        if (selected.Count == 0) return [];

        var ids = selected.Select(x => x.Id).ToList();
        var rows = await db.Customers.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var balances = await db.Invoices.AsNoTracking()
            .Where(i => ids.Contains(i.CustomerId) && (i.Status == InvoiceStatus.Open || i.Status == InvoiceStatus.PartiallyPaid))
            .GroupBy(i => i.CustomerId)
            .Select(g => new { g.Key, Balance = g.Sum(i => i.TotalAmount - i.PaidAmount) })
            .ToDictionaryAsync(x => x.Key, x => x.Balance, ct);

        // Fatura başka temsilcide kalmış ama müşteri artık görünmüyorsa satır atlanır (filtre zaten gizler).
        return ids.Where(rows.ContainsKey).Select(id =>
        {
            var c = rows[id];
            return new CustomerSync(c.Id, c.Code, c.Name, c.City, c.Address, c.Phone, c.Email, c.Latitude, c.Longitude,
                c.CreditLimit, balances.GetValueOrDefault(id));
        }).ToList();
    }

    private async Task<List<ProductSync>> ProductsAsync(ulong from, ulong upper, Pager page, CancellationToken ct)
    {
        // Pasif ürünler gönderilmez; pasifleşme tombstone olarak gider (ErpDbContext.SaveChanges).
        var rows = await db.Products.AsNoTracking()
            .Where(p => p.IsActive && EF.Property<ulong>(p, V) > from && EF.Property<ulong>(p, V) <= upper)
            .OrderBy(p => EF.Property<ulong>(p, V))
            .Take(page.Limit + 1)
            .Select(p => new
            {
                Version = EF.Property<ulong>(p, V),
                Item = new ProductSync(p.Id, p.Sku, p.Barcode, p.Name, p.Category.Name, p.Unit, p.UnitPrice, p.IsActive),
            })
            .ToListAsync(ct);
        return page.Take("product", rows, x => x.Version).Select(x => x.Item).ToList();
    }

    /// <summary>Aralıkta hareket gören ürünlerin tüm depolardaki güncel bakiyesi (sayfa birimi ürün).</summary>
    private async Task<List<StockSync>> StockAsync(ulong from, ulong upper, Pager page, CancellationToken ct)
    {
        var changed = await db.StockMovements.AsNoTracking()
            .Where(m => EF.Property<ulong>(m, V) > from && EF.Property<ulong>(m, V) <= upper)
            .GroupBy(m => m.ProductId)
            .Select(g => new { ProductId = g.Key, Version = g.Max(m => EF.Property<ulong>(m, V)) })
            .OrderBy(x => x.Version)
            .Take(page.Limit + 1)
            .ToListAsync(ct);
        var selected = page.Take("stock", changed, x => x.Version);
        if (selected.Count == 0) return [];

        var ids = selected.Select(x => x.ProductId).ToList();
        return await db.StockBalances.AsNoTracking()
            .Where(b => ids.Contains(b.ProductId))
            .OrderBy(b => b.ProductId).ThenBy(b => b.WarehouseId)
            .Select(b => new StockSync(b.ProductId, b.WarehouseId, b.Quantity))
            .ToListAsync(ct);
    }

    private async Task<List<OrderSync>> OrdersAsync(ulong from, ulong upper, DateTime since, Pager page, CancellationToken ct)
    {
        var rows = await db.Orders.AsNoTracking()
            .Where(o => o.OrderDate >= since && EF.Property<ulong>(o, V) > from && EF.Property<ulong>(o, V) <= upper)
            .OrderBy(o => EF.Property<ulong>(o, V))
            .Take(page.Limit + 1)
            .Select(o => new
            {
                Version = EF.Property<ulong>(o, V),
                Item = new OrderSync(o.Id, o.OrderNumber, o.ClientRequestId, o.CustomerId, o.WarehouseId, o.OrderDate,
                    o.Status, o.TotalAmount,
                    o.Lines.OrderBy(l => l.Id).Select(l => new OrderLineSync(l.ProductId, l.Product.Name, l.Quantity,
                        l.UnitPrice, l.DiscountRate, l.LineTotal, l.ShortQuantity)).ToList()),
            })
            .ToListAsync(ct);
        return page.Take("order", rows, x => x.Version)
            .Select(x => x.Item with { OrderDate = FieldClock.Utc(x.Item.OrderDate) })
            .ToList();
    }

    private async Task<List<VisitSync>> VisitsAsync(ulong from, ulong upper, DateOnly since, Pager page, CancellationToken ct)
    {
        var rows = await db.Visits.AsNoTracking()
            .Where(v => v.PlannedDate >= since && EF.Property<ulong>(v, V) > from && EF.Property<ulong>(v, V) <= upper)
            .OrderBy(v => EF.Property<ulong>(v, V))
            .Take(page.Limit + 1)
            .Select(v => new { Version = EF.Property<ulong>(v, V), Visit = v })
            .ToListAsync(ct);
        return page.Take(FieldTombstone.Visit, rows, x => x.Version).Select(x => FieldMapper.ToSync(x.Visit)).ToList();
    }

    private async Task<List<DeliverySync>> DeliveriesAsync(ulong from, ulong upper, DateOnly since, Pager page, CancellationToken ct)
    {
        var rows = await db.Deliveries.AsNoTracking()
            .Where(d => d.PlannedDate >= since && EF.Property<ulong>(d, V) > from && EF.Property<ulong>(d, V) <= upper)
            .OrderBy(d => EF.Property<ulong>(d, V))
            .Take(page.Limit + 1)
            .Select(d => new { Version = EF.Property<ulong>(d, V), Delivery = d })
            .ToListAsync(ct);
        var selected = page.Take(FieldTombstone.Delivery, rows, x => x.Version).Select(x => x.Delivery).ToList();
        return await FieldMapper.EnrichAsync(db, selected, ct);
    }

    private async Task<List<TombstoneSync>> TombstonesAsync(ulong from, ulong upper, Pager page, CancellationToken ct)
    {
        var isAdmin = user.Roles.Contains(Roles.Admin);
        var isRep = user.Roles.Contains(Roles.SalesRep);
        var repId = isRep ? user.SalesRepresentativeId ?? AccessPolicy.NoAccess : AccessPolicy.NoAccess;
        var driverId = user.Roles.Contains(Roles.Driver) ? user.DriverId ?? AccessPolicy.NoAccess : AccessPolicy.NoAccess;
        var global = isAdmin || isRep;

        // Tombstone'larda global filtre yok; kapsam burada açıkça kurulur:
        // - genel kayıtlar (ürün pasifleşti) temsilci ve yönetici için,
        // - görünürlük kaybı yalnızca o temsilci/sürücü için,
        // - yönetici ziyaret ve teslimat silinmelerini de alır (müşteri yeniden atamaları yöneticiyi etkilemez).
        var rows = await db.FieldTombstones.AsNoTracking()
            .Where(t => EF.Property<ulong>(t, V) > from && EF.Property<ulong>(t, V) <= upper)
            .Where(t => (global && t.SalesRepresentativeId == null && t.DriverId == null)
                        || (repId != AccessPolicy.NoAccess && t.SalesRepresentativeId == repId)
                        || (driverId != AccessPolicy.NoAccess && t.DriverId == driverId)
                        || (isAdmin && (t.Entity == FieldTombstone.Visit || t.Entity == FieldTombstone.Delivery)))
            .OrderBy(t => EF.Property<ulong>(t, V))
            .Take(page.Limit + 1)
            .Select(t => new { Version = EF.Property<ulong>(t, V), t.Entity, t.EntityId })
            .ToListAsync(ct);
        return page.Take("tombstone", rows, x => x.Version).Select(x => new TombstoneSync(x.Entity, x.EntityId)).ToList();
    }

    private sealed record Changed(int Id, ulong Version);

    /// <summary>
    /// Tür başına sayfa. Bir tür <c>limit</c>'i aşarsa o türün son döndürülen sürümü aday imleç olur;
    /// yanıt imleci kesilen türlerin en küçüğüdür. Kesilmeyen türlerin bu imleçten büyük satırları bir
    /// sonraki çağrıda tekrar gelir; istemci upsert yaptığından zararsızdır.
    /// </summary>
    private sealed class Pager(int limit)
    {
        private ulong? _cursor;

        public int Limit => limit;
        public bool HasMore => _cursor is not null;

        public List<T> Take<T>(string type, IReadOnlyList<T> sortedRows, Func<T, ulong> version)
        {
            if (sortedRows.Count <= limit) return sortedRows.ToList();

            var page = sortedRows.Take(limit).ToList();
            var last = version(page[^1]);
            _cursor = _cursor is null ? last : Math.Min(_cursor.Value, last);
            return page;
        }

        public ulong Cursor(ulong upper) => _cursor ?? upper;
    }
}
