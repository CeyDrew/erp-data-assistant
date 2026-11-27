# ADR 0006 — Microsoft.Extensions.AI ile sağlayıcıdan bağımsızlık

**Durum:** Kabul edildi

## Bağlam
Demo ücretsiz ve çevrimdışı çalışmalı (yerel Ollama), ama gerektiğinde bulut modelleri (OpenAI, Gemini,
Claude) aynı kodla denenebilmeli.

## Karar
Uygulama yalnızca `IChatClient` ve `IEmbeddingGenerator<string, Embedding<float>>` soyutlamalarını görür.
`ProviderChatClientFactory`, `Ai:Provider` değerine göre ham istemciyi üretir:

| Sağlayıcı | İstemci |
|---|---|
| Ollama (varsayılan) | `OllamaSharp.OllamaApiClient` |
| OpenAI | `OpenAI` SDK → `AsIChatClient()` |
| Gemini | Gemini'nin OpenAI uyumlu uç noktası + `OpenAI` SDK |
| Anthropic | resmi `Anthropic` SDK → `AsIChatClient()` |

Ham istemcinin önüne `ChatClientBuilder` ile ara katmanlar eklenir: seçenek ayarı (Ollama `num_ctx`),
`UseFunctionInvocation` (araç döngüsü, en fazla 6 tur), `UseLogging`.

## Sonuçlar
- Sağlayıcı değişimi yalnızca yapılandırma: `Ai__Provider=Anthropic`, `Ai__Anthropic__ApiKey=...`.
- Testler `IChatClientFactory`'yi deterministik sahte istemciyle değiştirir; araç döngüsü, yetki ve günlük
  gerçek koddan geçer.
- Embedding sağlayıcısı ayrı seçilir (Anthropic'in embedding API'si yok).
- Sağlayıcıya özgü özellikler (prompt caching, thinking ayarları) soyutlamanın dışında kalır; gerekirse
  `ChatOptions.RawRepresentationFactory` ile verilebilir.

## Alternatifler
- **Semantic Kernel:** daha geniş (planlayıcılar, bellek), ama bu proje için gereğinden ağır.
- **Her sağlayıcının SDK'sını doğrudan kullanmak:** araç çağrısı ve akış formatları sağlayıcıya göre değişir;
  dört ayrı döngü yazmak gerekir.
