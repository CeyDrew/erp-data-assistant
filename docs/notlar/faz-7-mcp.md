# Faz 7 — MCP sunucusu

## Ne yapıldı
- `ErpAssistant.Mcp`: resmi C# MCP SDK (`ModelContextProtocol.AspNetCore`), Streamable HTTP, durumsuz mod.
- Kişisel erişim token'ları: oluşturma/listeleme/iptal API'si ve panel sayfası; yalnızca SHA-256 özeti saklanır.
- 7 araç, sohbet asistanıyla aynı tanımlardan; her çağrı `mcp` kanalıyla günlüğe yazılır.
- 5 integration testi: araç listesi ve şemalar, rapor çağrısı + günlük, temsilci token'ının satır yetkisi,
  geçersiz/iptal edilmiş token.

## MCP ile REST farkı
| | REST API | MCP |
|---|---|---|
| Tüketici | Panel, entegrasyonlar | LLM ajanları |
| Keşif | OpenAPI dokümanı (insan/kod üretici okur) | `tools/list` — ad, açıklama, JSON Schema; model çalışma anında okur |
| Birim | Kaynak + HTTP fiili | Araç (fonksiyon) + argümanlar |
| Oturum | Durumsuz | Oturumlu veya durumsuz; bildirimler, ilerleme, örnekleme destekler |
| Sonuç | Uygulamaya özgü JSON | `content` blokları (metin, görüntü, kaynak), `isError` |

## Neden böyle
- **Durumsuz HTTP.** Araçlar salt okunur ve tek çağrılık; oturum durumu tutmaya gerek yok, yatay
  ölçeklenir. Sunucudan istemciye bildirim (örnekleme, ilerleme) gerekirse oturumlu moda geçilir.
- **PAT, OAuth değil.** MCP spesifikasyonu yetkilendirme için OAuth 2.1 tanımlıyor; yerel demo için kişisel
  token daha sade. Kurumsal kurulumda harici IdP ile OAuth akışı eklenir (SDK destekliyor).
- **Ayrı süreç.** MCP sunucusu API'den bağımsız ölçeklenir ve ayrı hız sınırı/izleme alabilir; ikisi de
  aynı Application/Infrastructure katmanlarını kullanır.

## Gözden geçirme soruları
- Bir MCP aracı ile bir REST uç noktası arasında tasarım farkı nedir? (açıklama kalitesi, parametre sayısı,
  sonuç boyutu, model için hata mesajları)
- MCP sunucusuna yazma aracı eklenseydi hangi önlemler gerekirdi? (onay, idempotency anahtarı, denetim izi)
- Token sızarsa etkisi nasıl sınırlanır? (kısa ömür, salt okunur kapsam, iptal, son kullanım izleme)
