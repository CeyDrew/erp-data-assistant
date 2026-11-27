using System.ComponentModel;
using System.Globalization;
using ErpAssistant.Application.Abstractions;
using ErpAssistant.Application.Common;
using ErpAssistant.Application.Inventory;
using ErpAssistant.Application.Orders;
using ErpAssistant.Application.Reports;
using ErpAssistant.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace ErpAssistant.Application.Ai;

public enum SatisGruplama { Gun, Ay, Kategori, Temsilci, Musteri }

public enum SiralamaOlcutu { Ciro, Miktar }

public enum SiparisDurumu { Acik, Tum, Taslak, Onayli, SevkEdildi, TeslimEdildi, Iptal }

/// <summary>
/// LLM'in çağırabileceği beyaz listeli, tipli araçlar. Serbest SQL yok: model yalnızca bu
/// metotlardan birini seçer ve parametreleri JSON Schema'ya göre doldurur. Her parametre burada
/// yeniden doğrulanır; veri, kullanıcının yetkisiyle filtrelenmiş servislerden gelir.
/// </summary>
public sealed class ErpTools(
    ReportService reports,
    StockService stock,
    OrderService orders,
    IErpDbContext db,
    ICurrentUser user,
    TimeProvider clock,
    ToolCallRecorder recorder)
{
    public const int MaxRows = 20;
    private const int MaxRangeDays = 3 * 366;

    [Description("Belirli bir tarih aralığındaki satışların (ciro ve sipariş sayısı) özetini verir. " +
                 "Ciro KDV hariçtir; taslak ve iptal siparişler dahil edilmez. grupla=Musteri en çok ciro yapan müşterileri, " +
                 "grupla=Temsilci temsilci performansını, grupla=Kategori kategori dağılımını sıralı verir.")]
    public Task<object> SatisOzeti(
        [Description("Başlangıç tarihi, yyyy-MM-dd")] string baslangic,
        [Description("Bitiş tarihi (dahil), yyyy-MM-dd")] string bitis,
        [Description("Gruplama: Gun, Ay, Kategori, Temsilci veya Musteri")] SatisGruplama grupla = SatisGruplama.Ay) =>
        recorder.RunAsync("satis_ozeti", new { baslangic, bitis, grupla }, async () =>
        {
            var (from, to) = Range(baslangic, bitis);
            var grouping = grupla switch
            {
                SatisGruplama.Gun => SalesGrouping.Day,
                SatisGruplama.Kategori => SalesGrouping.Category,
                SatisGruplama.Temsilci => SalesGrouping.SalesRepresentative,
                SatisGruplama.Musteri => SalesGrouping.Customer,
                _ => SalesGrouping.Month,
            };
            var s = await reports.SalesSummaryAsync(from, to, grouping, CancellationToken.None);
            var result = new
            {
                donem = $"{from:yyyy-MM-dd} / {to:yyyy-MM-dd}",
                siparisSayisi = s.OrderCount,
                toplamCiro = TL(s.Revenue),
                ortalamaSiparis = TL(s.AverageOrderValue),
                gruplar = s.Rows.Select(r => new { ad = r.Label, siparis = r.OrderCount, ciro = TL(r.Revenue) }),
                kesildi = s.Truncated,
            };
            return (result, s.Rows.Count);
        });

    [Description("Belirli bir dönemde en çok satan ürünleri ciroya veya miktara göre sıralar.")]
    public Task<object> EnCokSatanlar(
        [Description("Kaç ürün (1-20)")] int n,
        [Description("Başlangıç tarihi, yyyy-MM-dd")] string baslangic,
        [Description("Bitiş tarihi (dahil), yyyy-MM-dd")] string bitis,
        [Description("Sıralama ölçütü: Ciro veya Miktar")] SiralamaOlcutu olcut = SiralamaOlcutu.Ciro,
        [Description("İsteğe bağlı ürün kategorisi adı, örn. 'Vana'")] string? kategori = null) =>
        recorder.RunAsync("en_cok_satanlar", new { n, baslangic, bitis, olcut, kategori }, async () =>
        {
            if (n is < 1 or > MaxRows) throw new ToolArgumentException($"n 1 ile {MaxRows} arasında olmalı.");
            var (from, to) = Range(baslangic, bitis);

            int? categoryId = null;
            if (!string.IsNullOrWhiteSpace(kategori))
            {
                categoryId = await db.ProductCategories.Where(c => c.Name.Contains(kategori))
                    .Select(c => (int?)c.Id).FirstOrDefaultAsync()
                    ?? throw new ToolArgumentException($"'{kategori}' adında kategori yok.");
            }

            var financial = user.CanSeeFinancials;
            var metric = financial && olcut == SiralamaOlcutu.Ciro ? TopProductMetric.Revenue : TopProductMetric.Quantity;
            var rows = await reports.TopProductsAsync(n, from, to, metric, categoryId, CancellationToken.None);
            object result = new
            {
                donem = $"{from:yyyy-MM-dd} / {to:yyyy-MM-dd}",
                olcut = metric == TopProductMetric.Revenue ? "ciro" : "miktar",
                not = financial ? null : "Rolünüz ciro göremez; sıralama miktara göredir.",
                urunler = rows.Select(r => new
                {
                    sira = r.Rank, urunId = r.ProductId, sku = r.Sku, ad = r.Name, kategori = r.Category, miktar = r.Quantity,
                    ciro = financial ? TL(r.Revenue) : null,
                }),
            };
            return (result, rows.Count);
        });

    [Description("Belirli bir müşteriyi adına, koduna veya vergi numarasına göre arar ve musteriId döndürür. " +
                 "Soruda bir müşteri adı geçiyorsa ilk çağrılacak araç budur. Müşteri sıralaması için değil, tek müşteri bulmak içindir.")]
    public Task<object> MusteriAra(
        [Description("Müşteri adı veya adının bir kısmı, kodu (M-0012) ya da vergi numarası")] string arama) =>
        recorder.RunAsync("musteri_ara", new { arama }, async () =>
        {
            if (string.IsNullOrWhiteSpace(arama) || arama.Length > 100)
                throw new ToolArgumentException("Arama metni 1-100 karakter olmalı.");

            var term = arama.Trim();
            var rows = await db.Customers.AsNoTracking()
                .Where(c => c.Name.Contains(term) || c.Code == term || c.TaxNumber == term)
                .OrderBy(c => c.Name)
                .Take(10)
                .Select(c => new { musteriId = c.Id, kod = c.Code, ad = c.Name, sehir = c.City, temsilci = c.SalesRepresentative.FullName })
                .ToListAsync();

            object result = rows.Count == 0
                ? new { musteriler = rows, not = "Eşleşen müşteri yok veya bu müşteriyi görme yetkiniz yok." }
                : new { musteriler = rows, not = (string?)null };
            return (result, rows.Count);
        });

    [Description("Bir müşterinin siparişlerini listeler (en yeni önce). Müşteri adı, adının bir kısmı veya kodu verilir; " +
                 "sunucu müşteriyi bulur. Açık sipariş = taslak veya onaylı, henüz sevk edilmemiş.")]
    public Task<object> MusteriSiparisleri(
        [Description("Müşteri adı, adının ayırt edici kısmı (ör. 'Kahveci') veya kodu (ör. 'M-0012')")] string musteri,
        [Description("Durum filtresi: Acik, Tum, Taslak, Onayli, SevkEdildi, TeslimEdildi, Iptal")] SiparisDurumu durum = SiparisDurumu.Acik,
        [Description("En fazla kaç sipariş (1-20)")] int n = 10) =>
        recorder.RunAsync("musteri_siparisleri", new { musteri, durum, n }, async () =>
        {
            if (n is < 1 or > MaxRows) throw new ToolArgumentException($"n 1 ile {MaxRows} arasında olmalı.");
            var customer = await ResolveCustomerAsync(musteri);
            var musteriId = customer.Id;

            var query = new OrderQuery
            {
                CustomerId = musteriId,
                PageSize = n,
                OpenOnly = durum == SiparisDurumu.Acik ? true : null,
                Status = durum switch
                {
                    SiparisDurumu.Taslak => OrderStatus.Draft,
                    SiparisDurumu.Onayli => OrderStatus.Confirmed,
                    SiparisDurumu.SevkEdildi => OrderStatus.Shipped,
                    SiparisDurumu.TeslimEdildi => OrderStatus.Delivered,
                    SiparisDurumu.Iptal => OrderStatus.Cancelled,
                    _ => null,
                },
            };
            var page = await orders.ListAsync(query, CancellationToken.None);
            var result = new
            {
                musteri = customer.Name,
                toplamEslesen = page.TotalCount,
                gosterilen = page.Items.Count,
                siparisler = page.Items.Select(o => new
                {
                    siparisId = o.Id, no = o.OrderNumber, tarih = o.OrderDate.ToString("yyyy-MM-dd"), durum = o.Status.ToString(),
                    tutar = o.TotalAmount is { } t ? TL(t) : null, kalem = o.LineCount, depo = o.WarehouseCode,
                }),
            };
            return (result, page.Items.Count);
        });

    [Description("Ürünlerin depo bazında stok bakiyesini verir. Ürün adı/SKU, depo kodu veya yalnızca kritik seviyenin " +
                 "altındakilerle filtrelenebilir.")]
    public Task<object> StokDurumu(
        [Description("İsteğe bağlı ürün adı veya SKU parçası")] string? urun = null,
        [Description("İsteğe bağlı depo kodu: IST, ANK veya IZM")] string? depo = null,
        [Description("Yalnızca yeniden sipariş seviyesinin altındaki kalemler")] bool sadeceKritik = false) =>
        recorder.RunAsync("stok_durumu", new { urun, depo, sadeceKritik }, async () =>
        {
            if (depo is not null && depo is not ("IST" or "ANK" or "IZM"))
                throw new ToolArgumentException("Depo kodu IST, ANK veya IZM olmalı.");
            if (urun is { Length: > 100 }) throw new ToolArgumentException("Ürün araması en fazla 100 karakter.");

            var page = await stock.ListAsync(new StockQuery
            {
                Search = urun, Warehouse = depo, CriticalOnly = sadeceKritik, PageSize = MaxRows,
            }, CancellationToken.None);
            var result = new
            {
                toplamEslesen = page.TotalCount,
                gosterilen = page.Items.Count,
                kalemler = page.Items.Select(s => new
                {
                    urunId = s.ProductId, sku = s.Sku, urun = s.ProductName, depo = s.WarehouseCode, bakiye = s.Quantity,
                    kritikSeviye = s.ReorderLevel, kritik = s.IsCritical,
                }),
            };
            return (result, page.Items.Count);
        });

    [Description("Vadesi geçmiş, tamamen ödenmemiş faturaları (geciken tahsilatları) listeler.")]
    public Task<object> GecikenTahsilatlar(
        [Description("En az kaç gün gecikmiş olsun (0 = vadesi geçen tümü)")] int gun = 0,
        [Description("İsteğe bağlı müşteri adı veya kodu; yalnızca soru belirli bir müşteriyle ilgiliyse")] string? musteri = null) =>
        recorder.RunAsync("geciken_tahsilatlar", new { gun, musteri }, async () =>
        {
            if (gun is < 0 or > 3650) throw new ToolArgumentException("gun 0 ile 3650 arasında olmalı.");
            int? musteriId = string.IsNullOrWhiteSpace(musteri) ? null : (await ResolveCustomerAsync(musteri)).Id;
            var s = await reports.OverdueInvoicesAsync(gun, musteriId, CancellationToken.None);
            var rows = s.Rows.Take(MaxRows).ToList();
            var result = new
            {
                faturaSayisi = s.InvoiceCount,
                toplamKalan = TL(s.TotalOutstanding),
                gosterilen = rows.Count,
                faturalar = rows.Select(r => new
                {
                    no = r.InvoiceNumber, musteriId = r.CustomerId, musteri = r.CustomerName, temsilci = r.SalesRepresentative,
                    vade = r.DueDate.ToString("yyyy-MM-dd"), gecikmeGun = r.DaysOverdue, kalan = TL(r.Outstanding),
                }),
            };
            return (result, rows.Count);
        });

    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>
    /// Müşteriyi addan/koddan sunucuda çözer. Model kimlik uydurmaz; tek eşleşme yoksa açıklayıcı hata döner
    /// (birden fazlaysa adayları listeler, model kullanıcıya sorar).
    /// </summary>
    private async Task<(int Id, string Name)> ResolveCustomerAsync(string term)
    {
        term = term?.Trim() ?? "";
        if (term.Length is < 2 or > 100) throw new ToolArgumentException("Müşteri adı 2-100 karakter olmalı.");

        var exact = await db.Customers.AsNoTracking()
            .Where(c => c.Code == term || c.Name == term)
            .Select(c => new { c.Id, c.Name }).FirstOrDefaultAsync();
        if (exact is not null) return (exact.Id, exact.Name);

        // "Kahveci Endüstri'nin" gibi ekli ifadelerde ilk kelimeyle de dener.
        var firstWord = term.Split([' ', '\'', '’'], StringSplitOptions.RemoveEmptyEntries)[0];
        var matches = await db.Customers.AsNoTracking()
            .Where(c => c.Name.Contains(term) || c.Name.Contains(firstWord))
            .OrderBy(c => c.Name).Take(6)
            .Select(c => new { c.Id, c.Name, c.Code }).ToListAsync();

        return matches.Count switch
        {
            0 => throw new ToolArgumentException($"'{term}' ile eşleşen müşteri yok veya bu müşteriyi görme yetkiniz yok."),
            1 => (matches[0].Id, matches[0].Name),
            _ when matches.Count(m => m.Name.StartsWith(term, StringComparison.CurrentCultureIgnoreCase)) == 1 =>
                matches.Where(m => m.Name.StartsWith(term, StringComparison.CurrentCultureIgnoreCase)).Select(m => (m.Id, m.Name)).Single(),
            _ => throw new ToolArgumentException(
                $"'{term}' birden fazla müşteriyle eşleşti: {string.Join("; ", matches.Select(m => $"{m.Name} ({m.Code})"))}. " +
                "Kullanıcıya hangisini kastettiğini sor."),
        };
    }

    /// <summary>Tutar, modelin olduğu gibi aktarabileceği biçimde: "1.234.567,89 TL".</summary>
    private static string TL(decimal value) => value.ToString("#,##0.00", Tr) + " TL";

    private (DateTime From, DateTime To) Range(string from, string to)
    {
        var f = ParseDate(from, "baslangic");
        var t = ParseDate(to, "bitis");
        if (f > t) throw new ToolArgumentException("Başlangıç tarihi bitişten sonra olamaz.");
        if ((t - f).TotalDays > MaxRangeDays) throw new ToolArgumentException("Tarih aralığı en fazla 3 yıl olabilir.");
        var today = clock.GetUtcNow().UtcDateTime.Date;
        if (f > today) throw new ToolArgumentException("Başlangıç tarihi gelecekte olamaz.");
        return (f, t);
    }

    private static DateTime ParseDate(string value, string name) =>
        DateTime.TryParseExact(value?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : throw new ToolArgumentException($"{name} yyyy-MM-dd biçiminde olmalı (gelen: '{value}').");
}
