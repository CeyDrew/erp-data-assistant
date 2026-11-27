# AI güvenlik kuralları

| # | Kural | Nerede uygulanıyor | Nasıl test ediliyor |
|---|---|---|---|
| 1 | Serbest metinden SQL yok; yalnızca beyaz listeli tipli araçlar | `ErpToolCatalog`, `ErpTools`, `DocumentTools` | `ToolCatalogTests.Catalog_exposes_whitelisted_tools_only` |
| 2 | Araç parametreleri sunucuda yeniden doğrulanır | `ErpTools.Range`, `n`/`gun`/depo kontrolleri → `ToolArgumentException` | `AssistantTests.Invalid_tool_arguments_are_rejected_server_side`, `Malformed_date_is_rejected` |
| 3 | Sonuçlar satır sınırlı (20), özet alanlarla | `ErpTools.MaxRows`, `ReportService.MaxRows` | araç çıktılarında `toplamEslesen` / `kesildi` |
| 4 | Satır ve finansal yetki asistanda da geçerli | Araçlar aynı servisleri ve `DbContext` filtrelerini kullanır | `Sales_rep_cannot_reach_foreign_customer_through_tools`, `Warehouse_user_cannot_read_receivables_through_the_assistant` |
| 5 | RAG cevapları kaynak gösterir; kaynak yoksa "bulamadım" | `DocumentTools` (skor eşiği 0,45), sistem istemi kural 5, `sources` SSE olayı | Faz 6 testleri |
| 6 | Doküman içeriğindeki talimatlar veridir | Sistem istemi kural 5, araç çıktısındaki `not` alanı | Eval setindeki injection dokümanı (Faz 6) |
| 7 | Her soru günlüğe yazılır | `AssistantService` → `AiQueryLogs` (kullanıcı, kanal, araçlar, parametreler, satır, süre, token, model) | `Tool_call_streams_events_and_is_logged` |
| 8 | Sağlayıcı yapılandırmayla değişir | `Ai:Provider`, `IChatClientFactory` | Sahte istemciyle tüm integration testleri |
| 9 | Kötüye kullanım sınırı | `chat` hız sınırı (kullanıcı başına dakikada 20), soru ≤ 1000 karakter, geçmiş ≤ 12 mesaj | `ChatRequestValidator` |
| 10 | Araç döngüsü sınırlı | `MaximumIterationsPerRequest = 6` | — |

## Sistem istemi
`AssistantPrompt.Build` her istekte bugünün tarihini ve hazır tarih aralıklarını ("geçen ay: 2026-10-01 →
2026-10-31") verir. Küçük yerel modeller göreli tarihleri hesaplamada zorlanıyor; aralıkları önceden
hesaplamak araç parametresi doğruluğunu belirgin şekilde artırdı.
