# ADR 0002 — LLM'e serbest SQL yerine tipli araçlar

**Durum:** Kabul edildi

## Bağlam
"Doğal dilden SQL" (text-to-SQL) en esnek yaklaşım: model şemayı görür ve sorguyu yazar. Ancak:

- Model satır yetkisini atlayan bir sorgu üretebilir (`WHERE SalesRepresentativeId = ...` koşulunu unutması yeter).
- Prompt injection ile `DELETE`/`UPDATE` veya pahalı tam tablo taramaları tetiklenebilir.
- Üretilen SQL'in doğruluğunu (ör. iptal siparişlerin ciroya katılmaması) her seferinde denetlemek gerekir.
- Küçük yerel modeller karmaşık join'lerde sık hata yapar.

## Karar
Model yalnızca beyaz listedeki tipli araçlardan birini seçer: `satis_ozeti`, `en_cok_satanlar`, `musteri_ara`,
`musteri_siparisleri`, `stok_durumu`, `geciken_tahsilatlar`, `dokumanda_ara`.

- Araçlar `ErpTools` / `DocumentTools` metotlarıdır; JSON Schema metot imzasından ve `[Description]`
  özniteliklerinden `AIFunctionFactory` ile üretilir. Enum parametreler (`Gun|Ay|Kategori|...`) şemada
  `enum` olarak görünür.
- Her parametre sunucuda yeniden doğrulanır (tarih biçimi, aralık ≤ 3 yıl, `n ≤ 20`, depo kodu, departman).
  Doğrulama hatası modele `{ "hata": "..." }` olarak döner; model bunu kullanıcıya açıklar.
- Araçlar REST API'nin kullandığı aynı servisleri çağırır; satır filtresi ve finansal görünürlük kuralları
  otomatik uygulanır.
- Sonuçlar satır sınırlıdır (20) ve özet alanlar içerir (`toplamEslesen`, `kesildi`).

## Sonuçlar
- Asistan, kullanıcının REST API'de göremediği hiçbir veriyi döndüremez.
- Yeni bir soru tipi = yeni araç (kod + test). Esneklik azalır, öngörülebilirlik artar.
- Araç tanımı tek yerde; MCP sunucusu aynı listeyi yayınlar.
- Değerlendirme kolay: "doğru araç + doğru parametre" ölçülebilir (`tests/ErpAssistant.AiEvals`).

## Alternatifler
- **Text-to-SQL + salt okunur kullanıcı + view'lar:** yazma riski kalkar ama satır yetkisi için RLS ve
  sorgu maliyeti denetimi gerekir.
- **Semantic layer (metrik tanımları):** büyük ölçekte doğru yön; bu kapsam için fazla.
