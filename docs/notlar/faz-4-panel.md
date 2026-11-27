# Faz 4 — Next.js paneli

## Ne yapıldı
- Giriş ekranı (demo hesap seçici), gösterge paneli (KPI kartları, aylık ciro alan grafiği, kategori
  çubuk grafiği, en çok satanlar), sipariş/müşteri/fatura/ürün/stok listeleri ve detay sayfaları.
- Menü ve kolonlar role göre değişir: depo sorumlusu fiyat ve fatura menüsünü görmez; temsilciye
  "yalnızca size atanmış müşteriler" bilgisi gösterilir. Gizleme yalnızca kozmetik — asıl kural API'de.
- `scripts/screenshots.mjs`: Playwright ile üç rolde ekran görüntüsü.

## Neden böyle
- **Token yönetimi tek sınıfta** (`lib/api.ts`). Access token bellekte, refresh token localStorage'da.
  401 gelirse tek bir yenileme isteği yapılır; aynı anda düşen istekler aynı promise'i bekler, böylece
  rotasyonlu refresh token iki kez kullanılıp aile iptal edilmez.
- **TanStack Query**: önbellek anahtarı filtre nesnesi; `keepPreviousData` ile sayfa değişirken tablo
  boşalmaz.
- **Cache Components kapalı.** Tüm veri kullanıcıya özel ve tarayıcıda çekiliyor; sunucu tarafı
  önbelleklemenin kazancı yok, Suspense zorunlulukları ise karmaşıklık getiriyor.
- **Turkish_CI_AS** sayesinde liste sıralaması Türkçe alfabeye uygun (Ç, C'den sonra gelir).

## Alternatifler
- BFF (Next.js route handler'ları + httpOnly cookie): refresh token'ı XSS'ten korur; üretimde tercih
  edilir. Burada API'nin MCP ve diğer istemcilerle paylaşılan bearer modelini sade tutmak için seçilmedi.
- Sunucu bileşenleriyle veri çekme: token'ın sunucuya taşınmasını gerektirir (yine BFF).

## Gözden geçirme soruları
- localStorage'da token saklamanın XSS riski; httpOnly cookie hangi riski getirir (CSRF)?
- React Query'de `staleTime` ile `gcTime` farkı.
- Eşzamanlı 401'lerde refresh yarışını nasıl önlersiniz?
