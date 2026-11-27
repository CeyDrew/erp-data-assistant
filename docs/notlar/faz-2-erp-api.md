# Faz 2 — ERP REST API

## Ne yapıldı
- `api/v1` altında ürün, stok, müşteri, sipariş, fatura ve rapor uç noktaları (Asp.Versioning).
- Sayfalama (`page`, `pageSize ≤ 100`), filtreler, FluentValidation ile doğrulama.
- Merkezi hata yönetimi: `IExceptionHandler` → RFC 9457 ProblemDetails (404, 403, 422, 400, 500).
- OpenAPI dokümanı (`/openapi/v1.json`) ve Scalar arayüzü (`/scalar`).
- Veritabanı collation'ı `Turkish_CI_AS`: "istanbul" araması "İstanbul"u bulur.
- Testcontainers ile gerçek SQL Server üzerinde 11 integration testi.

## Neden böyle
- **Servisler `IQueryable` üzerinden DTO'ya projeksiyon yapar** (`Select(...)`). Entity yüklenmez, change
  tracker devreye girmez, yalnızca gereken kolonlar okunur; N+1 oluşmaz çünkü alt toplamlar
  (`o.Lines.Count`) aynı SQL'e gömülür.
- **Doğrulama filtrede.** Controller'lar yalnızca servisi çağırır; `IValidator<T>` kayıtlı her argüman
  action öncesi doğrulanır.
- **Rapor servisi tek yerde.** Panel, AI asistanı ve MCP aynı `ReportService` metotlarını çağıracak;
  satır sınırı (`MaxRows = 50`) ve finansal görünürlük kontrolü burada.
- **Gecikme günü bellekte hesaplanıyor.** `EF.Functions.DateDiffDay` SQL Server'a özgü; Application
  katmanı sağlayıcı paketine bağlanmasın diye sayfa (≤100 satır) geldikten sonra hesaplanır.
- **Testlerde tek konteyner, tek tohumlama** (`ICollectionFixture`): her test için veritabanı kurmak
  dakikalar sürerdi; testler salt okunur olduğu için paylaşmak güvenli.

## Alternatifler
- Minimal API + endpoint filter: daha az tören, ama filtre/versiyon/OpenAPI davranışları için controller
  daha olgun.
- Offset yerine keyset (cursor) sayfalama: büyük tablolarda derin sayfalar için daha hızlı; 20.000
  satırda offset yeterli ve UI'da sayfa numarası göstermek kolay.
- SQLite/InMemory ile test: hızlı ama indekslenmiş view, collation ve SQL çevirisi farkları yüzünden
  gerçeği yansıtmaz.

## Gözden geçirme soruları
- `IQueryable` ile `IEnumerable` farkı; `Where` hangi noktada SQL'e çevrilir?
- `AsNoTracking` ile `AsNoTrackingWithIdentityResolution` farkı.
- ProblemDetails ile ValidationProblemDetails farkı; neden `application/problem+json`?
- `WebApplicationFactory` uygulamayı nasıl ayağa kaldırır; neden `public partial class Program` gerekir?
