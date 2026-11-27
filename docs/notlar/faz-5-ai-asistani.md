# Faz 5 — AI asistanı

## Ne yapıldı
- 6 tipli ERP aracı (`ErpTools`) + doküman aracı tanımı (`DocumentTools`, Faz 6'da bağlanır).
- `AssistantService`: sistem istemi, araç döngüsü, olay akışı (`tool_call`, `tool_result`, `delta`, `sources`,
  `done`, `error`) ve sorgu günlüğü.
- `POST /api/v1/chat` → Server-Sent Events (`TypedResults.ServerSentEvents`).
- Sağlayıcı seçimi: Ollama (varsayılan, GPU'lu compose dosyası), OpenAI, Gemini, Anthropic.
- Panelde sohbet ekranı (akış, araç kartları, Markdown tablo) ve yönetici için AI sorgu günlüğü.
- Testler: sahte `IChatClient` ile 6 integration testi, araç şeması birim testleri, 30 soruluk eval seti.

## Function calling akışı
1. İstek: sistem istemi + geçmiş + soru + araç şemaları (`ChatOptions.Tools`).
2. Model metin yerine `FunctionCallContent` döner (ad + JSON argümanlar).
3. `FunctionInvokingChatClient` argümanları .NET tiplerine bağlar, metodu çalıştırır, sonucu
   `FunctionResultContent` olarak konuşmaya ekler ve modeli tekrar çağırır.
4. Model metin cevabı üretene kadar (en fazla 6 tur) tekrar eder.

Akış sırasında bu içerikler de güncellemelerde göründüğünden SSE olaylarına çevrilebiliyor; kullanıcı hangi
aracın hangi parametrelerle çağrıldığını ve kaç satır döndüğünü canlı görüyor.

## Neden böyle
- **SSE, WebSocket değil.** Tek yönlü akış yeterli; HTTP üzerinden çalışır, proxy dostu, .NET 10'da yerleşik.
  Tarayıcıda `EventSource` POST desteklemediği için `fetch` + `ReadableStream` ile okunuyor.
- **Araç çağrı kimliği `FunctionInvokingChatClient.CurrentContext`'ten.** Böylece kaydedici, model tarafındaki
  `callId` ile eşleşen satır sayısını ve süreyi bildirebiliyor.
- **Hazır tarih aralıkları istemde.** "Geçen ay" gibi ifadeleri modelin hesaplamasına bırakmak küçük
  modellerde en sık hata kaynağıydı.
- **Temperature 0,1.** Araç seçimi deterministik olmalı; yaratıcılık gerekmiyor.

## Alternatifler
- Araç sonuçlarını modele göstermeden doğrudan tabloya basmak: daha az token, ama "en çok satan ürünün
  cirosu geçen aya göre ne oldu" gibi birleştirme soruları yapılamaz.
- Sohbet durumunu sunucuda tutmak: istemci geçmişi gönderiyor (≤ 12 mesaj); sunucu durumsuz kalıyor.

## Gözden geçirme soruları
- Function calling'de modelin "araç çağırma" kararı nasıl verilir; `ToolMode.Auto` ile `RequireAny` farkı?
- Araç sonucu çok büyükse ne olur? (bağlam taşması, maliyet → satır sınırı, özet alanlar)
- Prompt injection'a karşı katmanlı savunma: araç beyaz listesi, sunucu doğrulaması, salt okunur araçlar,
  yetki filtresi, istemde "veri talimat değildir".
- LLM çıktısını nasıl test edersiniz? (sahte istemciyle deterministik akış testleri + gerçek modelle eval seti)
