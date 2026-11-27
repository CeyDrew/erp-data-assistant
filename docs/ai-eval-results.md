# Araç seçimi değerlendirmesi

Model: `Ollama/qwen2.5:7b` · Tarih: 2026-11-27

| # | Soru | Beklenen | Gelen | Sonuç |
|---|---|---|---|---|
| 1 | Geçen ay en çok satan 5 ürün hangileri? | `en_cok_satanlar` {"n":5,"baslangic":"2026-10-01","bitis":"2026-10-31"} | en_cok_satanlar {"baslangic":"2026-10-01","bitis":"2026-10-31","n":5,"olcut":"Ciro"} | ✅ |
| 2 | Bu yıl miktar olarak en çok satılan 10 ürünü listele. | `en_cok_satanlar` {"n":10,"olcut":"Miktar","baslangic":"2026-01-01"} | en_cok_satanlar {"bitis":"2026-11-27","n":10,"olcut":"Miktar","baslangic":"2026-01-01"} | ✅ |
| 3 | Geçen ayın toplam cirosu ne kadar? | `satis_ozeti` {"baslangic":"2026-10-01","bitis":"2026-10-31"} | satis_ozeti {"bitis":"2026-10-31","grupla":"Ay","baslangic":"2026-10-01"} | ✅ |
| 4 | Bu yılın aylık satışlarını göster. | `satis_ozeti` {"baslangic":"2026-01-01","grupla":"Ay"} | satis_ozeti {"baslangic":"2026-01-01","bitis":"2026-11-27","grupla":"Ay"} | ✅ |
| 5 | Geçen ay kategori bazında ciro dağılımı nasıl? | `satis_ozeti` {"baslangic":"2026-10-01","bitis":"2026-10-31","grupla":"Kategori"} | satis_ozeti {"grupla":"Kategori","baslangic":"2026-10-01","bitis":"2026-10-31"} | ✅ |
| 6 | Bu ay satış temsilcilerine göre satışlar | `satis_ozeti` {"baslangic":"2026-11-01","grupla":"Temsilci"} | satis_ozeti {"baslangic":"2026-11-01","bitis":"2026-11-27","grupla":"Temsilci"} | ✅ |
| 7 | Geçen yıl en çok ciro yaptığımız müşteriler kimler? | `satis_ozeti` {"baslangic":"2025-01-01","bitis":"2025-12-31","grupla":"Musteri"} | satis_ozeti {"baslangic":"2025-01-01","bitis":"2025-12-31","grupla":"Musteri"} | ✅ |
| 8 | Son 30 günün günlük satışları | `satis_ozeti` {"baslangic":"2026-10-28","grupla":"Gun"} | satis_ozeti {"baslangic":"2026-10-28","bitis":"2026-11-27","grupla":"Gun"} | ✅ |
| 9 | Kritik stok seviyesinin altındaki ürünler hangileri? | `stok_durumu` {"sadeceKritik":true} | stok_durumu {"sadeceKritik":true} | ✅ |
| 10 | İzmir deposunda kritik seviyedeki ürünleri göster. | `stok_durumu` {"depo":"IZM","sadeceKritik":true} | stok_durumu {"depo":"IZM","sadeceKritik":true} | ✅ |
| 11 | Ankara deposunda Küresel Vana stoğu ne kadar? | `stok_durumu` {"urun":"~k\u00FCresel vana","depo":"ANK"} | stok_durumu {"depo":"ANK","urun":"K\u00FCresel Vana"} | ✅ |
| 12 | Rulman stoklarımız ne durumda? | `stok_durumu` {"urun":"~rulman"} | stok_durumu {"urun":"Rulman"} | ✅ |
| 13 | Vadesi geçmiş faturaları listele. | `geciken_tahsilatlar` {} | geciken_tahsilatlar {"gun":0} | ✅ |
| 14 | 60 günden fazla gecikmiş tahsilatlar hangileri? | `geciken_tahsilatlar` {"gun":60} | geciken_tahsilatlar {"musteri":null,"gun":60} | ✅ |
| 15 | Vadesini 90 günden fazla aşmış alacaklarımız | `geciken_tahsilatlar` {"gun":90} | geciken_tahsilatlar {"gun":90,"musteri":null} | ✅ |
| 16 | Kahveci Endüstri'nin açık siparişleri neler? | `musteri_siparisleri` {"musteri":"~kahveci","durum":"Acik"} | musteri_siparisleri {"musteri":"Kahveci End\u00FCstri","durum":"A\u00E7\u0131k"} | ✅ |
| 17 | M-0012 kodlu müşteriyi bul. | `musteri_ara` {"arama":"~M-0012"} | musteri_ara {"arama":"M-0012"} | ✅ |
| 18 | Erez Yapı firmasının son siparişlerini göster. | `musteri_siparisleri` {"musteri":"~erez"} | musteri_siparisleri {"musteri":"Erez Yap\u0131","durum":"A\u00E7\u0131k","n":10} | ✅ |
| 19 | Bu yıl en çok satan 3 vana hangisi? | `en_cok_satanlar` {"n":3,"kategori":"~vana","baslangic":"2026-01-01"} | en_cok_satanlar {"baslangic":"2026-01-01","bitis":"2026-11-27","n":3,"kategori":"Vana","olcut":"Ciro"} | ✅ |
| 20 | Geçen ay miktar bazında en çok satılan 5 kablo | `en_cok_satanlar` {"n":5,"olcut":"Miktar","kategori":"~kablo","baslangic":"2026-10-01","bitis":"2026-10-31"} | en_cok_satanlar {"baslangic":"2026-10-01","bitis":"2026-10-31","n":5,"olcut":"Miktar","kategori":"Kablo"} | ✅ |
| 21 | Bu hafta kaç sipariş aldık? | `satis_ozeti` {"baslangic":"2026-11-23"} | musteri_siparisleri {"durum":"A\u00E7\u0131k","n":20,"baslangic":"2026-11-23","bitis":"2026-11-27"} | ❌ |
| 22 | 2026 Mart ayının satış özeti | `satis_ozeti` {"baslangic":"2026-03-01","bitis":"2026-03-31"} | satis_ozeti {"baslangic":"2026-03-01","bitis":"2026-03-31","grupla":"Ay"} | ✅ |
| 23 | Ocak 2026'de kategorilere göre satışlar | `satis_ozeti` {"baslangic":"2026-01-01","bitis":"2026-01-31","grupla":"Kategori"} | satis_ozeti {"grupla":"Kategori","baslangic":"2026-01-01","bitis":"2026-01-31"} | ✅ |
| 24 | Son 12 ayın aylık ciro trendi | `satis_ozeti` {"baslangic":"2025-12-01","grupla":"Ay"} | satis_ozeti {"baslangic":"2025-12-01","bitis":"2026-11-27","grupla":"Ay"} | ✅ |
| 25 | Hidrolik hortum stok durumu | `stok_durumu` {"urun":"~hortum"} | stok_durumu {"urun":"hortum","depo":"IST"} | ✅ |
| 26 | İstanbul deposundaki tüm kritik kalemler | `stok_durumu` {"depo":"IST","sadeceKritik":true} | stok_durumu {"depo":"IST","sadeceKritik":true} | ✅ |
| 27 | Geçen yıl en çok satan 10 ürün | `en_cok_satanlar` {"n":10,"baslangic":"2025-01-01","bitis":"2025-12-31"} | en_cok_satanlar {"olcut":"Ciro","baslangic":"2025-01-01","bitis":"2025-12-31","n":10} | ✅ |
| 28 | Bu ay en çok satan ürün hangisi? | `en_cok_satanlar` {"n":1,"baslangic":"2026-11-01"} | en_cok_satanlar {"olcut":"Ciro","baslangic":"2026-11-01","bitis":"2026-11-27","n":1} | ✅ |
| 29 | Tahsil edilemeyen, vadesi geçmiş alacaklarımızın toplamı nedir? | `geciken_tahsilatlar` {} | geciken_tahsilatlar {"gun":0} | ✅ |
| 30 | Garanti şartnamesine göre iade süresi kaç gün? | `dokumanda_ara` {"soru":"~iade"} | dokumanda_ara {"soru":"garanti \u015Fartname iade s\u00FCresi"} | ✅ |

**Sonuç: 29/30 (%97)** — eşik %90
