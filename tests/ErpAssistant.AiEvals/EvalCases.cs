namespace ErpAssistant.AiEvals;

/// <summary>
/// Soru → beklenen ilk araç ve parametreler. "~" ile başlayan değer "içerir" (büyük/küçük harf duyarsız),
/// diğerleri tam eşleşme. Belirtilmeyen parametreler serbest.
/// </summary>
public sealed record EvalCase(string Question, string Tool, Dictionary<string, object> Args);

public static class EvalCases
{
    public static IReadOnlyList<EvalCase> For(DateTime today)
    {
        string D(DateTime d) => d.ToString("yyyy-MM-dd");
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var lastMonthStart = monthStart.AddMonths(-1);
        var lastMonthEnd = monthStart.AddDays(-1);
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var yearStart = new DateTime(today.Year, 1, 1);
        var lastYearStart = new DateTime(today.Year - 1, 1, 1);
        var lastYearEnd = new DateTime(today.Year - 1, 12, 31);
        var y = today.Year;

        return
        [
            new("Geçen ay en çok satan 5 ürün hangileri?", "en_cok_satanlar",
                new() { ["n"] = 5, ["baslangic"] = D(lastMonthStart), ["bitis"] = D(lastMonthEnd) }),
            new("Bu yıl miktar olarak en çok satılan 10 ürünü listele.", "en_cok_satanlar",
                new() { ["n"] = 10, ["olcut"] = "Miktar", ["baslangic"] = D(yearStart) }),
            new("Geçen ayın toplam cirosu ne kadar?", "satis_ozeti",
                new() { ["baslangic"] = D(lastMonthStart), ["bitis"] = D(lastMonthEnd) }),
            new("Bu yılın aylık satışlarını göster.", "satis_ozeti",
                new() { ["baslangic"] = D(yearStart), ["grupla"] = "Ay" }),
            new("Geçen ay kategori bazında ciro dağılımı nasıl?", "satis_ozeti",
                new() { ["baslangic"] = D(lastMonthStart), ["bitis"] = D(lastMonthEnd), ["grupla"] = "Kategori" }),
            new("Bu ay satış temsilcilerine göre satışlar", "satis_ozeti",
                new() { ["baslangic"] = D(monthStart), ["grupla"] = "Temsilci" }),
            new("Geçen yıl en çok ciro yaptığımız müşteriler kimler?", "satis_ozeti",
                new() { ["baslangic"] = D(lastYearStart), ["bitis"] = D(lastYearEnd), ["grupla"] = "Musteri" }),
            new("Son 30 günün günlük satışları", "satis_ozeti",
                new() { ["baslangic"] = D(today.AddDays(-30)), ["grupla"] = "Gun" }),
            new("Kritik stok seviyesinin altındaki ürünler hangileri?", "stok_durumu",
                new() { ["sadeceKritik"] = true }),
            new("İzmir deposunda kritik seviyedeki ürünleri göster.", "stok_durumu",
                new() { ["depo"] = "IZM", ["sadeceKritik"] = true }),
            new("Ankara deposunda Küresel Vana stoğu ne kadar?", "stok_durumu",
                new() { ["urun"] = "~küresel vana", ["depo"] = "ANK" }),
            new("Rulman stoklarımız ne durumda?", "stok_durumu",
                new() { ["urun"] = "~rulman" }),
            new("Vadesi geçmiş faturaları listele.", "geciken_tahsilatlar", new()),
            new("60 günden fazla gecikmiş tahsilatlar hangileri?", "geciken_tahsilatlar",
                new() { ["gun"] = 60 }),
            new("Vadesini 90 günden fazla aşmış alacaklarımız", "geciken_tahsilatlar",
                new() { ["gun"] = 90 }),
            new("Kahveci Endüstri'nin açık siparişleri neler?", "musteri_ara",
                new() { ["arama"] = "~kahveci" }),
            new("M-0012 kodlu müşteriyi bul.", "musteri_ara",
                new() { ["arama"] = "~M-0012" }),
            new("Erez Yapı firmasının son siparişlerini göster.", "musteri_ara",
                new() { ["arama"] = "~erez" }),
            new("Bu yıl en çok satan 3 vana hangisi?", "en_cok_satanlar",
                new() { ["n"] = 3, ["kategori"] = "~vana", ["baslangic"] = D(yearStart) }),
            new("Geçen ay miktar bazında en çok satılan 5 kablo", "en_cok_satanlar",
                new() { ["n"] = 5, ["olcut"] = "Miktar", ["kategori"] = "~kablo", ["baslangic"] = D(lastMonthStart), ["bitis"] = D(lastMonthEnd) }),
            new("Bu hafta kaç sipariş aldık?", "satis_ozeti",
                new() { ["baslangic"] = D(weekStart) }),
            new($"{y} Mart ayının satış özeti", "satis_ozeti",
                new() { ["baslangic"] = $"{y}-03-01", ["bitis"] = $"{y}-03-31" }),
            new($"Ocak {y}'de kategorilere göre satışlar", "satis_ozeti",
                new() { ["baslangic"] = $"{y}-01-01", ["bitis"] = $"{y}-01-31", ["grupla"] = "Kategori" }),
            new("Son 12 ayın aylık ciro trendi", "satis_ozeti",
                new() { ["baslangic"] = D(monthStart.AddMonths(-11)), ["grupla"] = "Ay" }),
            new("Hidrolik hortum stok durumu", "stok_durumu",
                new() { ["urun"] = "~hortum" }),
            new("İstanbul deposundaki tüm kritik kalemler", "stok_durumu",
                new() { ["depo"] = "IST", ["sadeceKritik"] = true }),
            new("Geçen yıl en çok satan 10 ürün", "en_cok_satanlar",
                new() { ["n"] = 10, ["baslangic"] = D(lastYearStart), ["bitis"] = D(lastYearEnd) }),
            new("Bu ay en çok satan ürün hangisi?", "en_cok_satanlar",
                new() { ["n"] = 1, ["baslangic"] = D(monthStart) }),
            new("Tahsil edilemeyen, vadesi geçmiş alacaklarımızın toplamı nedir?", "geciken_tahsilatlar", new()),
            new("Garanti şartnamesine göre iade süresi kaç gün?", "dokumanda_ara",
                new() { ["soru"] = "~iade" }),
        ];
    }
}
