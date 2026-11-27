# ADR 0008 — Mobil saha modülü: aynı serviste modül, rowversion imleci, idempotency tablosu

**Durum:** Kabul edildi

## Bağlam
Satış temsilcisi (sipariş, ziyaret) ve teslimat sürücüsü (teslimat, kanıt) için çevrimdışı çalışan bir
Flutter uygulaması ([field-sales-app](https://github.com/CeyDrew/field-sales-app)) yazıldı. Uygulama
ERP verisinin yerel bir kopyasını tutuyor, bağlantı olmadığında işlemleri kuyruğa alıyor ve bağlantı
gelince gönderiyor. Bundan sunucu için dört ihtiyaç çıktı:

1. **Delta senkron:** "son senkrondan beri ne değişti" sorusuna kaçırmadan ve tekrar etmeden cevap.
2. **Tekrar gönderime dayanıklı yazma:** mobil ağda istek gider, yanıt kaybolur, istemci tekrar dener.
   Aynı sipariş iki kez oluşmamalı.
3. **Sürücü yetkisi:** sürücü yalnızca kendine atanmış teslimatları görmeli; müşteri listesi, fiyat ve
   diğer siparişler ona açılmamalı.
4. Mevcut satır yetkisi (ADR 0003) ve domain kuralları aynen geçerli kalmalı.

## Karar

### Ayrı servis değil, aynı API içinde `Field` modülü
Uç noktalar `/api/v1/field/*` altında, `FieldController` + `Application/Field` servisleri. Sipariş,
müşteri, stok ve fatura zaten bu veritabanında; ayrı bir servis bu tabloları ya paylaşacak (gizli
bağımlılık) ya da olay ile kopyalayacaktı (senkron gecikmesi, iki doğruluk kaynağı). Modül aynı
`IErpDbContext`'i, aynı global query filter'ları ve aynı domain metotlarını (`Order.AddLine`,
`Order.MarkDelivered`) kullanıyor; kural iki yerde tutulmuyor. Yük ayrışırsa modülün sınırı (kendi
controller'ı, servisleri, tabloları) ayrı bir servise taşımayı kolaylaştırıyor.

### Delta imleci: SQL Server `rowversion` + `MIN_ACTIVE_ROWVERSION() - 1`
Customer, Product, Order, StockMovement, Invoice, Payment, Visit, Delivery ve FieldTombstone
tablolarına `rowversion` gölge kolonu (`Version`) eklendi; domain sınıfları değişmedi. İmleç bu
değerin 16 haneli hex yazımı. `UpdatedAt` yerine rowversion seçildi: saat kayması ve aynı milisaniyede
iki değişiklik sorunu yok, uygulama kodunun güncellemeyi unutma ihtimali yok, `ExecuteUpdate` ve ham
SQL de değeri değiştiriyor.

Rowversion **commit sırasında değil yazma anında** atanır. İmleç olarak `@@DBTS` (son verilen değer)
kullanılsaydı şu senaryo veri kaybettirirdi: A işlemi 100'ü alır ama commit etmez, B işlemi 101'i alıp
commit eder, istemci 101'e kadar okur ve imleci 101 yapar; A sonra commit edince 100 numaralı satır
imlecin altında kalır ve bir daha hiç gönderilmez. Bu yüzden üst sınır yanıt başında okunan
`MIN_ACTIVE_ROWVERSION() - 1`: hâlâ açık olan en küçük rowversion'ın bir altı. Bu sınırın altındaki her
satır commit edilmiştir; açık işlemlerin satırları bir sonraki senkrona kalır. Sorgular `(since, upper]`
aralığını okur.

- **Müşteri bakiyesi** fatura ve tahsilattan hesaplandığından müşteri, kendi satırı *ya da* faturası /
  tahsilatı değiştiyse gelir; sayfalamada müşterinin sürümü bu üçünün en büyüğüdür.
- **Stok** hareket defterinden geldiği için, aralıkta hareket gören ürünlerin tüm depolardaki bakiyesi
  (`vw_StockBalance`) gönderilir.
- **Sayfalama** tür başına `limit`; bir tür kesilirse yanıt imleci kesilen türlerin son sürümlerinin en
  küçüğü olur. Kesilmeyen türlerin bu değerin üstündeki satırları tekrar gelir; istemci upsert yaptığı
  için zararsız.
- **Tombstone:** silinen ya da görünürlükten çıkan satırlar (müşteri başka temsilciye geçti, ürün
  pasifleşti, ziyaret/teslimat silindi veya yeniden atandı) `ErpDbContext.SaveChanges` içinde, değişiklikle
  aynı işlemde `FieldTombstones` tablosuna yazılır; tablo da rowversion ile okunur. Aynı yanıtta satırın
  kendisi de varsa tombstone düşürülür, istemcinin uygulama sırası önemsizleşir.

### Idempotency: `IdempotencyRecords` tablosu
`POST /field/orders` `Idempotency-Key` başlığı ister; `/field/sync/push` işlemlerinde anahtar işlem `id`'si.
Başarılı sonuç (durum kodu + JSON yanıt + istek gövdesinin SHA-256 özeti) anahtarla saklanır:

- Aynı anahtar + aynı gövde → saklanan yanıt aynen döner (`Idempotent-Replayed: true`), işlem çalışmaz.
- Aynı anahtar + farklı gövde → `422 idempotency_key_reused`.
- Fiyat değiştiyse (`409 price_changed`) hiçbir şey saklanmaz; istemci güncel fiyatla aynı anahtarı
  tekrar kullanabilir.
- **Eşzamanlı iki aynı istek:** ikisi de "kayıt yok" görür. Sipariş ve kayıt tek işlemde yazılır;
  `Orders.ClientRequestId` ve kayıt anahtarı üzerindeki benzersiz index'ler ikinci INSERT'i birincinin
  commit'ine kadar bekletir ve ihlalle döndürür. Kaybeden işlem geri alınır, kazananın kaydını okuyup
  aynı yanıtı döner. Sonuç: tek sipariş, iki istemciye aynı yanıt.

Redis gibi ayrı bir depo yerine tablo seçildi: kayıt iş verisiyle aynı işlemde yazılıyor, "sipariş
oluştu ama anahtar yazılmadı" ara durumu yok.

### Sürücü satır yetkisi
Yeni `Surucu` rolü ve JWT'de `driver_id` claim'i. `AccessPolicy.RowFilterFor` sürücü için `NoAccess`
döndürmeye devam ediyor: REST, asistan ve MCP'de müşteri/sipariş/fatura görünürlüğü **genişletilmedi**.
Teslimatlara ayrı bir global filtre uygulanıyor (`AccessPolicy.DriverFilterFor`): sürücü yalnızca
kendine atanmışları, temsilci kendi siparişlerininkileri (denormalize `SalesRepresentativeId`),
yönetici ve depo hepsini görür.

Teslimat kartında müşteri adı, adres, konum ve kalemler gerekiyor. Bunlar teslimat filtresinden geçmiş
satırların sipariş kimlikleriyle sınırlı bir sorguda `IgnoreQueryFilters()` ile okunur ve yalnızca bu
alanlar projekte edilir (`FieldMapper.EnrichAsync`). ADR 0003'teki "Application katmanında
`IgnoreQueryFilters()` yok" kuralının bilinçli ve yorumlu iki istisnası bu zenginleştirme ve sunucu içi
demo planlayıcıdır; ikisi de kimlik listesini filtrelenmiş bir kaynaktan alır.

## Sonuçlar
- Mevcut tablolara rowversion eklemek bir kerelik bir migration (`FieldModule`); indekslenmiş
  `vw_StockBalance` view'u yeni kolonu kullanmadığı için etkilenmedi.
- Rowversion eşzamanlılık belirteci yalnızca Visit ve Delivery'de açık (iki cihaz aynı teslimatı
  tamamlarsa biri `conflict` alır); diğer tablolarda mevcut yazma davranışı değişmedi.
- Sipariş numarası `SM-yyyy-NNNNNN` bir veritabanı sequence'inden; boşluk olabilir (geri alınan işlem),
  tekrar olmaz.
- Demo planlayıcı, kullanıcı senkron yaptığında bugünün planı yoksa deterministik ziyaret/teslimat
  üretir. Gerçek kurulumda bu tabloları rota planlama sistemi doldurur; API değişmez.

## Alternatifler
- **`UpdatedAt` zaman damgası:** saat kayması, aynı anda iki değişiklik, unutulan güncelleme ve aynı
  commit-sırası sorunu.
- **SQL Server Change Tracking:** satır bazında değişikliği verir ama satır filtresiyle (temsilci) ve
  türetilmiş alanlarla (bakiye) birleştirmek zor; saklama süresi dolunca tam senkron gerekir.
- **Olay akışı (outbox + istemciye event log):** en esnek, ama bu ölçekte fazla hareketli parça.
