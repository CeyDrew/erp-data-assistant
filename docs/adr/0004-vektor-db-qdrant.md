# ADR 0004 — Vektör veritabanı olarak Qdrant

**Durum:** Kabul edildi

## Bağlam
Doküman parçalarının embedding'leri saklanmalı ve departman filtresiyle benzerlik araması yapılmalı.
Adaylar: Qdrant, PostgreSQL + pgvector, SQL Server 2025 `vector` tipi.

## Karar
Qdrant (Docker, gRPC). Her embedding modeli için ayrı koleksiyon (`documents_bge_m3`); payload'da
`documentId`, `fileName`, `department`, `page`, `chunkIndex`, `text`. `department` ve `documentId`
alanlarında payload index var. Arama `department ∈ izinli departmanlar` filtresi ve kosinüs benzerlik
eşiğiyle (`Qdrant:MinScore`) yapılır.

Erişim resmi `Qdrant.Client` ile, uygulamanın kendi `IDocumentSearch` / `IVectorIndex` arayüzlerinin
arkasında. `Microsoft.Extensions.VectorData` soyutlaması değerlendirildi; Qdrant bağlayıcısı önizleme
sürümündeydi ve `MatchAny` gibi filtrelerin ifadeye çevrilmesi garanti değildi. Arayüz zaten uygulamanın
kendi sınırı olduğundan bağlayıcı olgunlaşınca değiştirmek tek sınıfı etkiler.

## Sonuçlar
- Filtreli HNSW araması; departman filtresi arama sırasında uygulanır (sonradan eleme değil), `top-k`
  doğru kalır.
- Ek bir servis; ama ERP veritabanını vektör yüküyle büyütmüyor.
- Embedding modeli değişince yeni koleksiyon oluşur; eski vektörler karışmaz, yeniden işleme gerekir.

## Alternatifler
- **pgvector:** ikinci bir veritabanı motoru (PostgreSQL) getirirdi.
- **SQL Server 2025 vector:** tek veritabanı avantajı; ancak geliştirici imajı ve EF Core desteği bu projenin
  yazıldığı dönemde yeni, ANN index'i önizleme.
