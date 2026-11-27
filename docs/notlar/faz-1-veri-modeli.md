# Faz 1 — Domain ve veri modeli

## Ne yapıldı
- 14 entity, `IEntityTypeConfiguration` sınıfları, ilk migration.
- `vw_StockBalance` indekslenmiş view'ı migration içinde ham SQL ile.
- Bogus ile deterministik sentetik veri (≈25 sn'de 20.000 sipariş).
- Domain kuralları için birim testleri (sipariş yaşam döngüsü, KDV, tahsilat, stok işareti).

## Neden böyle
- **Kurallar entity'de.** `Order.AddLine`, `Invoice.RegisterPayment` gibi metotlar geçersiz durumu
  daha oluşmadan engeller; aynı kural API'de, seeder'da ve testte tek yerden gelir.
- **Bakiye saklanmıyor.** Hareket defteri tek doğruluk kaynağı; bakiye kolonu olsaydı her hareketle
  senkron tutmak (ve yarış koşulları) gerekirdi. İndekslenmiş view bu toplamı veritabanına bırakır.
- **Enum'lar `byte`.** 20.000 sipariş için fark küçük ama index sayfalarına daha çok satır sığar.
- **`decimal(18,2)` ve string uzunlukları konvansiyonla** (`ConfigureConventions`): her kolonda tekrar yok,
  `nvarchar(max)` sızmıyor.
- **Seeder'da `ChangeTracker.Clear()` ve 2.000'lik partiler.** Change tracker büyüdükçe `DetectChanges`
  O(n) maliyet getirir; partiler arası temizlik bellek ve süreyi sabit tutar.

## Alternatifler
- `SqlBulkCopy` daha hızlı olurdu ama domain metotlarını atlardı. 25 sn kabul edilebilir.
- Bakiye için trigger + tablo: yazma maliyeti benzer, mantık veritabanında gizli kalır.
- SQL Server Standard'da indekslenmiş view'ı kullanmak için sorguda `WITH (NOEXPAND)` gerekir;
  Developer/Enterprise optimizer'ı bunu kendisi seçer.

## Gözden geçirme soruları
- `AsNoTracking` ne zaman kullanılır, ne kazandırır?
- Covering index nedir, `INCLUDE` kolonları neden anahtar kolonlardan farklı?
- İndekslenmiş view'ın şartları neler? (`SCHEMABINDING`, `COUNT_BIG(*)`, deterministik ifadeler, SET ayarları)
- EF Core'da `Add` bir nesne grafını nasıl işler; ana veri nesnesi neden yeniden insert edilmeye çalışılır?
