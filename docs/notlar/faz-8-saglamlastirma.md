# Faz 8 — Sağlamlaştırma

## Ne yapıldı
- **OpenTelemetry:** ASP.NET Core, HttpClient, EF Core, LLM çağrıları (`UseOpenTelemetry`, GenAI semantik
  kuralları), araç çağrıları ve doküman işleme için izler; araç/doküman sayaçları ve süre histogramları.
  Compose'da Aspire paneline OTLP ile gider.
- **Sağlık kontrolleri:** `/health` (liveness), `/health/ready` (SQL Server, RabbitMQ + dead-letter sayısı,
  Qdrant sürümü, Ollama modelleri indirilmiş mi).
- **Index ölçümü:** `docs/performance.md`. "En çok satanlar" sorgusundaki tam index taraması bulundu;
  FK index'i kapsayıcı yapıldı (421 → 211 okuma).
- **Tek komutla kurulum:** `docker compose up -d --build` → 10 servis (veritabanı, kuyruk, vektör DB, LLM,
  model indirici, Aspire, API, worker, MCP, panel). GPU için ayrı override dosyası.
- README, mimari dokümanı, ekran görüntüleri.

## Telemetriden çıkan iki düzeltme
1. **Model değişimi gecikmesi.** Aspire izinde doküman sorusunun 12 sn'sinin 7,7'sinin sorgu embedding'i olduğu
   görüldü: Ollama varsayılan olarak tek modeli bellekte tutuyor, sohbet modeli ile embedding modeli arasında her
   soruda yükleme/boşaltma yapıyordu. `OLLAMA_MAX_LOADED_MODELS=2` ile ikisi birlikte GPU'da kalıyor
   (aynı adım ≈ 0,2 sn).
2. **Aramadan "bulamadım".** Sorgu günlüğünde bazı doküman sorularının hiç araç çağrılmadan 0,4 sn'de
   cevaplandığı görüldü. Anahtar kelimeye dayalı bir yönlendirme notu (kullanıcı mesajına eklenir) bunu
   giderdi. Not ayrı bir sistem mesajı olarak eklendiğinde qwen2.5'in sohbet şablonu bozuluyor ve araç
   çağrısını düz metin olarak yazıyordu; bu yüzden kullanıcı mesajına eklendi.

## Bilinen sınırlar
- 7B yerel model doğru parçayı bulsa da bazen yanlış okuyabiliyor (ör. "%10–20 arası bölge müdürü" kuralında
  %15 için "genel müdür" dedi). Kaynak gösterimi kullanıcının doğrulamasını kolaylaştırıyor; daha büyük bir
  model (`AI_PROVIDER=Anthropic`) bu tür okuma hatalarını belirgin şekilde azaltır.
- Windows + Docker Desktop'ta `localhost` IPv6 yönlendirmesi zaman zaman takılabiliyor; servis içi
  yapılandırmalarda `127.0.0.1` kullanıldı.

## Gözden geçirme soruları
- Liveness ile readiness kontrolü neden ayrı? (Orkestratör yeniden başlatma ile trafik kesme kararları farklı.)
- Bir LLM isteğinin izinde hangi span'leri görmek istersiniz? (model, token, araç, veritabanı, vektör arama)
- Kapsayıcı index'in yazma maliyeti nedir, ne zaman değmez?
