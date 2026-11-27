# ADR 0007 — MediatR / MassTransit yerine sade servisler ve RabbitMQ.Client

**Durum:** Kabul edildi

## Bağlam
.NET ekosisteminde CQRS için MediatR, mesajlaşma için MassTransit yaygın. İkisi de 2025'te ticari
lisans modeline geçti; ayrıca küçük bir kod tabanında dolaylılık katmanı ekliyor.

## Karar
- Use case'ler Application katmanında sade servis sınıfları (`OrderService`, `ReportService`, ...).
  Controller → servis çağrısı doğrudan; doğrulama `ValidationFilter`'da, hata eşleme `IExceptionHandler`'da.
- Mesajlaşma `RabbitMQ.Client` 7 (async API) ile; topoloji, retry ve dead-letter açıkça kodda (ADR 0005).

## Sonuçlar
- "Go to definition" doğrudan iş koduna gider; pipeline davranışları (doğrulama, günlük) ASP.NET Core'un
  kendi filtre/middleware mekanizmalarında.
- Retry, DLQ, onaylar gibi MassTransit'in hazır verdiği davranışlar elle yazıldı ve test edildi
  (`Corrupt_document_fails_after_retries_and_goes_to_dead_letter`).
