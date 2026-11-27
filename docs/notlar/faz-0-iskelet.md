# Faz 0 — İskelet

## Ne yapıldı
- `ErpAssistant.slnx` (yeni XML tabanlı solution formatı) altında 6 kaynak, 3 test projesi.
- Ortak derleme ayarları `Directory.Build.props` içinde; SDK sürümü `global.json` ile sabit.
- Docker Compose: `mssql`, `rabbitmq`, `qdrant`, `ollama`, `api`. MSSQL ve RabbitMQ health check'li,
  API onlar sağlıklı olunca kalkıyor.
- API: Serilog istek günlüğü, OpenAPI + Scalar arayüzü (`/scalar`), `/health`.
- GitHub Actions: restore, build, test.

## Neden böyle
- **Bağımlılık yönü içe doğru.** `Domain` hiçbir şeye, `Application` yalnızca `Domain`'e bağlı.
  Altyapı (EF Core, RabbitMQ, LLM) değiştirilebilir ayrıntı olarak dışta kalıyor.
- **Üç giriş noktası (Api, Worker, Mcp) aynı Infrastructure'ı paylaşıyor.** Kural ve yetki tek yerde.
- **`public partial class Program`** — `WebApplicationFactory<Program>` ile integration testlerin
  API'yi bellekte ayağa kaldırabilmesi için.
- **Bootstrap logger** — host kurulmadan önceki hatalar da (ör. bozuk yapılandırma) günlüğe düşer.

## Alternatifler
- Minimal API yerine controller: sürüm, filtre ve ProblemDetails davranışı daha tanıdık; uç nokta sayısı
  büyüdükçe gruplama kolay.
- Tek proje (vertical slice) daha az tören ister; burada katman sınırlarını derleyici zorlasın istendi.

## Gözden geçirme soruları
- `AddSingleton`, `AddScoped`, `AddTransient` farkı; scoped servisi singleton'a enjekte etmek neden hata?
- Middleware sırası neden önemli? (`UseAuthentication` → `UseAuthorization` → endpoint)
- Health check'te liveness ile readiness farkı.
