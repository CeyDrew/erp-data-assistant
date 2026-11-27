# ADR 0005 — Doküman işlemenin kuyruk + worker ile asenkron yapılması

**Durum:** Kabul edildi

## Bağlam
Büyük bir PDF'in metin ayıklama + parçalama + embedding süresi saniyelerden dakikalara çıkabilir.
Bunu HTTP isteği içinde yapmak zaman aşımı, API thread'lerinin bloklanması ve hata durumunda yeniden
deneme zorluğu demek.

## Karar
API dosyayı saklar, `Documents` tablosuna `Pending` kaydı ekler, RabbitMQ'ya `DocumentUploaded` mesajı
atar ve `202 Accepted` döner. Ayrı `Worker` süreci kuyruğu tüketir.

Topoloji (`documents` direct exchange):

```
documents.process ──hata──▶ documents.retry (TTL 5 sn) ──süre dolunca──▶ documents.process
        │
        └── deneme > 3 ──▶ documents.dead (dead-letter)  +  doküman "Hata"
```

- `prefetch = 1`, elle ack: worker çökerse mesaj kaybolmaz.
- Deneme sayısı `x-attempt` başlığında taşınır; son hata `x-last-error` başlığında.
- İşleme idempotent: yeniden işlemede eski Qdrant noktaları ve `DocumentChunks` satırları silinir.
- Yayıncı onayları (publisher confirms) açık.

## Sonuçlar
- API hızlı kalır; worker yatay ölçeklenebilir (aynı kuyrukta birden fazla tüketici).
- Panel durumu (`Kuyrukta → İşleniyor → Hazır/Hata`) polling ile izler.
- Dead-letter kuyruğu manuel inceleme için kalır; panelden "yeniden işle" mümkün.

## Alternatifler
- **Hangfire / Quartz:** veritabanı tabanlı iş kuyruğu; ek altyapı yok ama mesajlaşma desenini göstermez.
- **MassTransit:** retry/DLQ hazır gelir; lisans değişikliği (v9) ve soyutlama maliyeti nedeniyle doğrudan
  `RabbitMQ.Client` tercih edildi (ADR 0007).
- **`Channel<T>` + BackgroundService (süreç içi):** süreç yeniden başlarsa iş kaybolur.
