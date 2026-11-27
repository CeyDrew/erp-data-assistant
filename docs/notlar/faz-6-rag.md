# Faz 6 — RAG ve doküman hattı

## Ne yapıldı
- Yükleme API'si (PDF, DOCX, XLSX, TXT, MD · ≤ 20 MB), departman yetkisi kontrolü, `202 Accepted`.
- RabbitMQ topolojisi: işleme, gecikmeli yeniden deneme (3 deneme) ve dead-letter kuyrukları.
- Worker: metin ayıklama (PdfPig, OpenXml) → sayfa sınırını aşmayan ~800 token'lık, 100 token örtüşmeli
  parçalar → embedding (Ollama `bge-m3`, çok dilli) → Qdrant.
- `dokumanda_ara` aracı: departman filtresi + benzerlik eşiği; kaynaklar SSE `sources` olayıyla panele gider.
- Prompt injection için iki katman: sistem istemi kuralı + `InjectionGuard` (talimat cümlelerini gizler).
- Örnek dokümanlar `tools/ErpAssistant.SampleDocs` ile üretilip `samples/documents` altında; API ilk
  açılışta kuyruğa atar. Biri bilerek gömülü talimat içerir.
- Testler: Testcontainers ile RabbitMQ + Qdrant üzerinde uçtan uca yükleme → işleme → kaynaklı cevap,
  departman filtresi, retry/dead-letter; gerçek modelle injection değerlendirmesi.

## Neden böyle
- **Parçalar sayfa sınırını aşmıyor.** Kaynak gösterimi "doküman, sayfa" olarak kesin kalıyor.
- **Örtüşme.** Cümle iki parçanın sınırına denk gelirse ikisinde de tam görünür.
- **bge-m3.** Türkçe dahil çok dilli; `nomic-embed-text` gibi İngilizce ağırlıklı modellere göre Türkçe
  sorgularda belirgin şekilde daha iyi eşleşme.
- **Departman filtresi Qdrant'ta.** Sonradan elemek `top-k`'yı bozar (5 sonuçtan 4'ü elenirse 1 kalır).
- **İkinci savunma katmanı.** Gerçek modelle yapılan değerlendirmede 7B model istemdeki kurala rağmen bir
  koşuda dokümandaki talimata uyup başka bir aracı çağırdı. Yetki filtresi veri sızıntısını zaten engelliyordu,
  ama davranış yanlıştı; talimat cümlelerini modele göndermeden ayıklamak bunu kapattı (3/3 koşu).

## Alternatifler
- Sabit karakter yerine semantik parçalama (başlık/paragraf yapısına göre): daha iyi bağlam, daha karmaşık.
- Hibrit arama (BM25 + vektör): ürün kodu gibi tam eşleşme gereken sorgularda daha iyi; Qdrant sparse
  vektörlerle destekliyor, sonraki adım olabilir.
- Re-ranker (cross-encoder): kaliteyi artırır, gecikmeyi de.

## Gözden geçirme soruları
- Parça boyutu neden önemli? (küçük: bağlam kaybı; büyük: alakasız metin, token maliyeti, embedding seyrelmesi)
- Kosinüs benzerliği eşiği nasıl seçilir? (modele göre değişir; etiketli küçük bir set ile)
- At-least-once teslimatta işlemi idempotent yapmak; aynı mesaj iki kez gelirse ne olur?
- Dead-letter kuyruğundaki mesajlar nasıl yeniden işlenir?
- Prompt injection'a karşı neden tek bir önlem yetmez?
