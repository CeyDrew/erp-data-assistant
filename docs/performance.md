# Sorgu performansı ve index'ler

Ölçüm: `docs/perf/benchmarks.sql`, SQL Server 2022 Developer (Docker), sentetik veri (20.000 sipariş,
≈54.000 kalem, ≈19.000 fatura, ≈54.000 stok hareketi). Her sorgu bir kez ısındırıldıktan sonra
`SET STATISTICS IO, TIME ON` ile ölçüldü. "Tarama" satırı aynı sorgunun `WITH (INDEX(0))` ile zorlanmış
clustered index taramasıdır (ilgili index yokmuş gibi).

| Sorgu | Kullanılan yapı | Mantıksal okuma | Tarama ile | Süre (ms) |
|---|---|---:|---:|---:|
| Son 12 ayın aylık cirosu | `IX_Orders_OrderDate_Covering` (OrderDate + INCLUDE Status, TotalAmount, …) | **71** | 246 | 12 / 13 |
| Geçen ayın en çok satan 10 ürünü | `IX_Orders_OrderDate_Covering` + `IX_OrderLines_OrderId` (kapsayıcı) | **9 + 211** | 246 + 421 | 7 / 17 |
| Temsilcinin son 3 ay siparişleri | `IX_Orders_SalesRepresentativeId_OrderDate` | **6** | 246 | <1 / 3 |
| Vadesi 60 günü geçmiş açık faturalar | `IX_Invoices_Status_DueDate` (INCLUDE tutarlar) | **54** | 301 | <1 / 4 |
| Kritik stok kalemleri | `vw_StockBalance` indekslenmiş view | **17 + 11** | 532 + 11 | <1 / 31 |

## Ölçümden çıkan değişiklik
İlk ölçümde "en çok satanlar" sorgusu, tarih filtresiyle 9 sayfada bulunan siparişlere rağmen
`OrderLines` tarafında 421 sayfalık `IX_OrderLines_ProductId` index'ini baştan sona tarıyordu: EF Core'un
foreign key için oluşturduğu `IX_OrderLines_OrderId` yalnızca `OrderId` içerdiğinden, kalemin ürün, miktar ve
tutarına ulaşmak için ya ana tabloya dönmesi (key lookup) ya da daha geniş index'i taraması gerekiyordu.

FK index'i kapsayıcı yapıldı (`OrderId` + INCLUDE `ProductId, Quantity, LineTotal`, migration
`CoveringOrderLinesOrderIdIndex`). Sonuç: **421 → 211 okuma, 20 → 7 ms**. Optimizer hâlâ hash join seçiyor
(bir aylık ≈2.500 kalem için 900 ayrı seek, tek dar taramadan pahalı), ama taradığı yapı yarı boyutta.

## Notlar
- Satır yetkisi filtresi (`SalesRepresentativeId = @rep`) denormalize kolon üzerinden index'le çalışıyor
  (3. satır); filtre join gerektirseydi temsilci sorguları müşteri tablosuna bağımlı kalırdı.
- İndekslenmiş view, stok bakiyesini her hareket insert'inde günceller. Okuma 543 → 28 sayfa; bedeli her
  stok hareketi yazımında view index'inin de güncellenmesi.
- Developer/Enterprise sürümünde optimizer indekslenmiş view'ı kendiliğinden eşleştirir; Standard sürümde
  sorguda `WITH (NOEXPAND)` gerekir.
- Bu veri hacminde tüm sorgular zaten milisaniyeler içinde; fark asıl olarak veri büyüdükçe (okuma sayısı
  doğrusal büyür) ve eşzamanlı yükte (buffer pool baskısı) önem kazanır.

## Tekrar üretmek

```bash
docker cp docs/perf/benchmarks.sql erp-assistant-mssql-1:/tmp/b.sql
docker exec erp-assistant-mssql-1 /opt/mssql-tools18/bin/sqlcmd -I -S localhost -U sa \
  -P 'Erp_Asistan_2026!' -C -d ErpAssistant -i /tmp/b.sql
```
