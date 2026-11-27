using ErpAssistant.Domain.Common;

namespace ErpAssistant.Domain.Field;

/// <summary>Mobil cihazdan gelen konum noktası. <see cref="ClientId"/> istemcide üretilir; tekrar gönderim tekilleşir.</summary>
public sealed class LocationPing
{
    public long Id { get; set; }
    public required string UserId { get; set; }
    public Guid ClientId { get; set; }
    public DateTime RecordedAt { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Accuracy { get; set; }
}

/// <summary>Kullanıcının mobil kurulumu (bildirim token'ı ve uygulama sürümü takibi için).</summary>
public sealed class Device : Entity
{
    public required string UserId { get; set; }
    public required string InstallationId { get; set; }
    public required string Platform { get; set; }
    public required string AppVersion { get; set; }
    public string? PushToken { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
}

/// <summary>
/// Başarıyla uygulanmış bir istemci işleminin sonucu. Aynı anahtar tekrar gelirse işlem yeniden
/// çalıştırılmaz; saklanan yanıt döner. <see cref="RequestHash"/> farklıysa anahtar başka bir istek için
/// yeniden kullanılmış demektir.
/// </summary>
public sealed class IdempotencyRecord
{
    public Guid Key { get; set; }
    public required string UserId { get; set; }
    public required string Operation { get; set; }

    /// <summary>İstek gövdesinin SHA-256 özeti (hex).</summary>
    public required string RequestHash { get; set; }

    public int StatusCode { get; set; }
    public required string ResponseJson { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Mobil istemcinin yerel kopyasından silmesi gereken satır: kayıt silindi, pasifleşti ya da kullanıcının
/// görünürlüğünden çıktı. Temsilci ve sürücü alanları boşsa kayıt herkes için geçerlidir.
/// </summary>
public sealed class FieldTombstone
{
    public const string Customer = "customer";
    public const string Product = "product";
    public const string Visit = "visit";
    public const string Delivery = "delivery";

    public long Id { get; set; }
    public required string Entity { get; set; }
    public int EntityId { get; set; }

    /// <summary>Kaydı artık göremeyen temsilci (müşteri/ziyaret yeniden atandıysa eski temsilci).</summary>
    public int? SalesRepresentativeId { get; set; }

    /// <summary>Kaydı artık göremeyen sürücü.</summary>
    public int? DriverId { get; set; }

    public DateTime CreatedAt { get; set; }
}
