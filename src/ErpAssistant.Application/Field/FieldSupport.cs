using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ErpAssistant.Application.Field;

/// <summary>Saha modülünün SQL Server'a özgü ihtiyaçları (Infrastructure'da uygulanır).</summary>
public interface IFieldDatabase
{
    /// <summary>
    /// Senkron üst sınırı: <c>MIN_ACTIVE_ROWVERSION() - 1</c>. Bu değerin altındaki her rowversion commit
    /// edilmiş bir işleme aittir; açık bir işlemin satırı sınırın üstünde kalır ve bir sonraki senkrona
    /// bırakılır. Sınır yerine <c>@@DBTS</c> kullanılsaydı, uzun süren bir işlemin küçük rowversion'lı satırı
    /// commit edildiğinde imleç onu çoktan geçmiş olurdu ve satır istemciye hiç gitmezdi.
    /// </summary>
    Task<ulong> SyncUpperBoundAsync(CancellationToken ct);

    /// <summary>Mobil sipariş numarası için veritabanı sequence'inden sıradaki değer.</summary>
    Task<long> NextOrderSequenceAsync(CancellationToken ct);

    /// <summary>Sevkte kesilen fatura numarası (FS-yyyy-NNNNNN) için sequence.</summary>
    Task<long> NextInvoiceSequenceAsync(CancellationToken ct);

    /// <summary>İşi tek bir veritabanı işleminde, bağlantı yeniden deneme stratejisine uygun şekilde çalıştırır.</summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct);

    bool IsUniqueViolation(DbUpdateException exception);

    /// <summary>İzlenen tüm değişiklikleri bırakır (başarısız bir işlemden sonra bağlamı temizlemek için).</summary>
    void DiscardChanges();
}

/// <summary>Delta senkron sürüm kolonu: SQL Server rowversion, gölge özellik olarak (domain temiz kalır).</summary>
public static class FieldVersion
{
    public const string Property = "Version";

    public static ulong Of<T>(T entity) where T : class => EF.Property<ulong>(entity, Property);
}

public static class FieldCursor
{
    public static ulong Parse(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor) || cursor == "0") return 0;
        if (cursor.Length > 16 || !ulong.TryParse(cursor, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            throw new ValidationException([new ValidationFailure("since", "İmleç 16 haneli onaltılık bir değer olmalı.")]);
        return value;
    }

    public static string Format(ulong value) => value.ToString("X16", CultureInfo.InvariantCulture);
}

public static class FieldJson
{
    /// <summary>MVC çıktısıyla aynı biçim: camelCase, enum'lar string.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static JsonElement ToElement<T>(T value) => JsonSerializer.SerializeToElement(value, Options);

    public static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    public static T Payload<T>(JsonElement payload) where T : class
    {
        if (payload.ValueKind != JsonValueKind.Object)
            throw new ValidationException([new ValidationFailure("payload", "İşlem gövdesi bir JSON nesnesi olmalı.")]);
        try
        {
            return payload.Deserialize<T>(Options)
                ?? throw new ValidationException([new ValidationFailure("payload", "İşlem gövdesi boş.")]);
        }
        catch (JsonException ex)
        {
            throw new ValidationException([new ValidationFailure("payload", $"İşlem gövdesi okunamadı: {ex.Message}")]);
        }
    }

    public static string Sha256(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>
    /// Sipariş isteğinin kanonik özeti. Ondalıklar ölçekten bağımsız yazılır (129.9 = 129.90), böylece
    /// istemci aynı isteği farklı biçimlendirse de aynı istek sayılır.
    /// </summary>
    public static string Hash(FieldOrderRequest request)
    {
        var sb = new StringBuilder()
            .Append(request.CustomerId).Append('|')
            .Append(request.WarehouseId).Append('|')
            .Append(request.Note?.Trim()).Append('|');
        foreach (var line in request.Lines)
        {
            sb.Append(line.ProductId).Append(':').Append(line.Quantity).Append(':')
                .Append(line.UnitPrice.ToString("0.############", CultureInfo.InvariantCulture)).Append(':')
                .Append(line.DiscountRate.ToString("0.############", CultureInfo.InvariantCulture)).Append(';');
        }
        return Sha256(sb.ToString());
    }
}

public static class FieldClock
{
    /// <summary>Türkiye saati (UTC+3, yaz saati uygulaması yok); ziyaret ve teslimat planları bu güne göre.</summary>
    public static readonly TimeSpan TurkeyOffset = TimeSpan.FromHours(3);

    public static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime + TurkeyOffset);

    public static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public static DateTime? Utc(DateTime? value) => value is { } v ? Utc(v) : null;
}

/// <summary>EAN-13 barkod: 869 (Türkiye) ön eki + ürün kimliği + kontrol hanesi.</summary>
public static class Ean13
{
    public static string ForProduct(int productId)
    {
        var body = "869" + productId.ToString("D9", CultureInfo.InvariantCulture);
        return body + CheckDigit(body);
    }

    public static int CheckDigit(string twelveDigits)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++)
            sum += (twelveDigits[i] - '0') * (i % 2 == 0 ? 1 : 3);
        return (10 - sum % 10) % 10;
    }

    public static bool IsValid(string code) =>
        code.Length == 13 && code.All(char.IsAsciiDigit) && CheckDigit(code) == code[12] - '0';
}
