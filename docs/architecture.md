# Mimari

```mermaid
flowchart LR
  U[Kullanıcı] --> W[Next.js paneli]
  W -->|REST + SSE| A[ASP.NET Core API]
  AG[Dış ajan<br/>Claude Code vb.] -->|MCP · Streamable HTTP| M[MCP sunucusu]
  A --> AP[Application<br/>servisler + AI araçları]
  M --> AP
  AP --> DB[(SQL Server<br/>ERP verisi)]
  AP --> V[(Qdrant<br/>doküman vektörleri)]
  AP --> L[LLM / embedding<br/>Ollama · Gemini · OpenAI · Claude]
  A -->|DocumentUploaded| Q[[RabbitMQ]]
  Q --> WK[Worker<br/>ayıkla → parçala → embed]
  WK --> V
  WK --> L
  A & M & WK -.->|OTLP| O[Aspire paneli]
```

## Bir sorunun yolculuğu

```mermaid
sequenceDiagram
  participant P as Panel
  participant API as ChatController
  participant AS as AssistantService
  participant FI as FunctionInvokingChatClient
  participant LLM as Model (Ollama)
  participant T as ErpTools
  participant DB as SQL Server

  P->>API: POST /api/v1/chat (JWT)
  API->>AS: AskAsync(soru, geçmiş)
  AS->>FI: sistem istemi + araç şemaları
  FI->>LLM: istek
  LLM-->>FI: FunctionCall en_cok_satanlar {n:5, baslangic, bitis}
  FI-->>P: SSE tool_call
  FI->>T: EnCokSatanlar(5, ...)  (parametre doğrulama)
  T->>DB: LINQ → SQL (global query filter: temsilci)
  DB-->>T: satırlar
  T-->>FI: { urunler: [...] }
  FI-->>P: SSE tool_result (5 satır, 160 ms)
  FI->>LLM: araç sonucu
  LLM-->>FI: metin akışı
  FI-->>P: SSE delta …
  AS->>DB: AiQueryLogs (araçlar, satır, süre, token)
  AS-->>P: SSE done
```

## Projeler

| Proje | Sorumluluk |
|---|---|
| `ErpAssistant.Domain` | Entity'ler, iş kuralları (sipariş yaşam döngüsü, KDV, tahsilat, stok hareketi işareti) |
| `ErpAssistant.Application` | Servisler (ürün, stok, müşteri, sipariş, fatura, rapor, doküman), AI araçları ve istem, `AccessPolicy`, arayüzler |
| `ErpAssistant.Infrastructure` | EF Core + migration'lar + tohumlama, Identity/JWT/PAT, RabbitMQ, Qdrant, LLM ve embedding sağlayıcıları, metin ayıklama, OpenTelemetry, sağlık kontrolleri |
| `ErpAssistant.Api` | REST + SSE, ProblemDetails, versiyonlama, hız sınırı, OpenAPI/Scalar |
| `ErpAssistant.Worker` | Doküman kuyruğu tüketicisi |
| `ErpAssistant.Mcp` | MCP sunucusu |
| `web/` | Next.js paneli |
| `tools/ErpAssistant.SampleDocs` | Örnek şirket dokümanlarını üretir |

## Yetki modeli

| Rol | Satırlar | Finansal veri | Dokümanlar |
|---|---|---|---|
| Yönetici | hepsi | evet | hepsi |
| Satış temsilcisi | yalnızca kendi müşterileri, siparişleri, faturaları | evet | Satış, Kalite |
| Depo sorumlusu | tüm siparişler (sevkiyat için), stok | hayır (fiyat, ciro, fatura gizli) | Depo, Kalite |

Kuralların tek kaynağı `AccessPolicy`. EF Core global query filter'ları, servis projeksiyonları, AI
araçları, vektör araması ve MCP aynı değerleri okur. Ayrıntı: [ADR 0003](adr/0003-satir-yetkisi-global-query-filter.md).

## Gözlemlenebilirlik
- **İzler:** ASP.NET Core, HttpClient, EF Core, LLM çağrıları (OpenTelemetry GenAI semantik kuralları:
  model, token), her araç çağrısı (`tool <ad>` span'i, dönen satır) ve doküman işleme.
- **Metrikler:** `erp.ai.tool_calls`, `erp.ai.tool_duration`, `erp.documents.processed`,
  `erp.documents.duration`, LLM token kullanımı, istek/çalışma zamanı metrikleri.
- **Sağlık:** `/health` (süreç), `/health/ready` (SQL Server, RabbitMQ + dead-letter sayısı, Qdrant, Ollama
  modelleri indirilmiş mi).
- Compose'da Aspire paneli: http://localhost:18888
