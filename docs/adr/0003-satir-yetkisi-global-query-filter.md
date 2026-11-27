# ADR 0003 — Satır yetkisi için EF Core global query filter

**Durum:** Kabul edildi

## Bağlam
Satış temsilcisi yalnızca kendi müşterilerini, siparişlerini ve faturalarını görmeli. Aynı veri üç
yoldan okunuyor: REST API, AI asistanının araçları ve MCP sunucusu. Filtreyi her sorguya elle eklemek,
bir yerde unutulduğunda veri sızıntısı demek; AI araçlarında bu risk daha yüksek, çünkü sorguyu hangi
parametrelerle çalıştıracağına model karar veriyor.

## Karar
Filtreler `ErpDbContext.OnModelCreating` içinde `HasQueryFilter` ile tanımlanır ve
`AccessPolicy.RowFilterFor(ICurrentUser)` değerini okur. Application katmanı veriye yalnızca
`IErpDbContext` üzerinden erişir; AI ve MCP araçları da aynı servisleri çağırır.

- Bilinmeyen rol veya kimliksiz istek **sıfır satır** görür (fail-closed, `NoAccess = -1`).
- Sistem işlemleri (migration, tohumlama, worker) `SystemUser` ile filtresiz çalışır.
- Finansal kolonlar (fiyat, ciro) satır filtresi değil, projeksiyonda `CanSeeFinancials` ile gizlenir;
  fatura uç noktaları ayrıca `financials` politikasıyla korunur.

## Sonuçlar
- Bir geliştiricinin filtreyi unutması mümkün değil; `IgnoreQueryFilters()` çağrısı kod incelemesinde
  göze batar ve Application katmanında hiç kullanılmaz.
- `Orders` ve `Invoices` üzerinde `SalesRepresentativeId` denormalize edildi; filtre join'siz, index'le
  çalışır.
- Filtre değeri sorgu parametresi olduğundan plan önbelleği kullanıcılar arasında paylaşılır.
- Ham SQL (`FromSql`) kullanılırsa filtre yine uygulanır (EF sarmalayıcı sorgu üretir) ama
  `ExecuteSql` gibi komutlar filtrelenmez; bu yüzden yazma tarafında ham SQL kullanılmaz.

## Alternatifler
- **SQL Server Row-Level Security** (`SESSION_CONTEXT` + güvenlik politikası): veritabanı seviyesinde
  daha güçlü garanti, ama bağlantı havuzunda oturum bağlamını her istekte ayarlamak ve test etmek zor.
  İleride savunma katmanı olarak eklenebilir.
- **Repository katmanında elle filtre:** her yeni sorguda unutulma riski.
