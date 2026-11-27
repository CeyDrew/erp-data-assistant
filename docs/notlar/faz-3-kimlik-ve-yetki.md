# Faz 3 — Kimlik ve yetki

## Ne yapıldı
- ASP.NET Core Identity tabloları (`IdentityDbContext<AppUser>`), üç rol: `Yonetici`, `SatisTemsilcisi`,
  `DepoSorumlusu`. Demo kullanıcıları başlangıçta oluşturulur (şifre `Demo123!`).
- JWT access token (15 dk) + dönen refresh token (7 gün). Refresh token'ın yalnızca SHA-256 özeti
  saklanır; kullanılmış bir token tekrar gelirse aynı ailedeki tüm token'lar iptal edilir.
- `HasQueryFilter` ile satır yetkisi (bkz. ADR 0003); dokümanlarda departman filtresi.
- `financials` politikası, giriş uç noktalarında IP başına hız sınırı.
- 9 yeni integration testi: temsilci başka temsilcinin müşterisini id ile bile açamıyor, depo sorumlusu
  fiyat göremiyor, refresh token yeniden kullanımı aileyi kapatıyor.

## Neden böyle
- **Kısa access + uzun refresh.** Access token doğrulaması veritabanına gitmez (stateless); iptal ihtiyacı
  refresh tarafında karşılanır. 15 dakika, çalınan bir token'ın penceresini sınırlar.
- **Rotasyon + yeniden kullanım tespiti.** Refresh token çalınırsa saldırgan ya da gerçek kullanıcı eski
  token'ı tekrar kullanacaktır; bu an tespit edilip aile kapatılır (OAuth 2.0 BCP önerisi).
- **`MapInboundClaims = false`.** `sub`, `role` gibi kısa claim adları Microsoft'un uzun URI'lerine
  dönüştürülmez; token okunabilir kalır.
- **Fail-closed.** Rol eşlemesinde bilinmeyen durum "her şeyi gör" değil "hiçbir şey görme" sonucunu verir.

## Alternatifler
- Cookie + ASP.NET Identity UI: tek alan adında daha basit; ama Next.js ayrı origin ve MCP/ajan istemcileri
  için bearer token daha uygun.
- Harici IdP (Keycloak, Entra ID): gerçek kurumda doğru seçim; lokal demo için ek servis ve yapılandırma.

## Gözden geçirme soruları
- JWT neden iptal edilemez, bunun pratik çözümleri neler? (kısa ömür, deny-list, refresh rotasyonu)
- `UseAuthentication` ve `UseAuthorization` neden bu sırada ve endpoint eşlemesinden önce?
- Global query filter'ı ne zaman `IgnoreQueryFilters` ile atlarsınız; bunun riski?
- Rol tabanlı ile politika (claim) tabanlı yetkilendirme farkı.
