# ADR 0001 — Clean Architecture ve proje sınırları

**Durum:** Kabul edildi

## Bağlam
Aynı iş kuralları ve yetki modeli üç farklı giriş noktasından kullanılıyor: panelin REST API'si, doküman
işleyen worker ve ajanlara açılan MCP sunucusu. Kuralların bir giriş noktasına gömülmesi, diğerlerinde
tekrar yazılmasına (ve farklılaşmasına) yol açar.

## Karar

```
Domain          entity'ler ve iş kuralları — hiçbir pakete bağımlı değil
Application     use case servisleri, DTO'lar, AI araçları, yetki politikası, arayüzler (IErpDbContext, IDocumentSearch…)
Infrastructure  EF Core + SQL Server, Identity/JWT/PAT, RabbitMQ, Qdrant, LLM sağlayıcıları, doküman okuyucular, OpenTelemetry
Api / Worker / Mcp   yalnızca giriş noktası: HTTP, kuyruk tüketicisi, MCP taşıyıcısı
```

Bağımlılık yönü içe doğru: `Api, Worker, Mcp → Infrastructure → Application → Domain`. Derleyici bu yönü
zorlar (proje referansları).

Pragmatik istisnalar:
- Application, `IQueryable` üzerinden projeksiyon yapabilmek için EF Core'un soyut paketine bağlı
  (sağlayıcı paketine değil). Repository katmanı eklemek her sorguyu iki kez tanımlamak demekti.
- Application, AI araç tanımları için `Microsoft.Extensions.AI` soyutlamalarına bağlı; sağlayıcı paketleri
  (OllamaSharp, OpenAI, Anthropic) yalnızca Infrastructure'da.

## Sonuçlar
- Satır yetkisi, finansal görünürlük ve araç doğrulaması tek yerde; üç giriş noktası aynı davranır.
- Testler katman bazında: Domain kuralları birim testle, servisler + filtreler gerçek SQL Server'la,
  AI akışı sahte `IChatClient` ile.
- Bedeli: küçük bir proje için daha fazla proje ve dosya.

## Alternatifler
- **Vertical slice (özellik klasörleri, tek proje):** daha az tören; ama Worker ve Mcp'nin aynı kodu
  paylaşması için yine ortak bir kütüphane gerekirdi.
- **Tek proje + klasörler:** katman ihlallerini derleyici yakalamaz.
