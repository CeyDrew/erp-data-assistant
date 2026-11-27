using ErpAssistant.Api.Infrastructure;
using ErpAssistant.Application.Field;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpAssistant.Api.Controllers;

/// <summary>
/// Mobil saha uygulaması (satış temsilcisi ve teslimat sürücüsü) için çevrimdışı öncelikli API.
/// Sözleşme: field-sales-app deposundaki docs/api-contract.md.
/// </summary>
public sealed class FieldController(
    FieldSyncService sync,
    FieldOrderService orders,
    FieldPushService push,
    FieldDeviceService devices) : ApiControllerBase
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";
    public const string ReplayedHeader = "Idempotent-Replayed";
    private const long MaxProofRequestBytes = 7 * 1024 * 1024;

    /// <summary>
    /// Delta senkron: <paramref name="since"/> imlecinden bu yana değişen satırlar (ilk senkronda boş/0).
    /// Rowversion tabanlı; üst sınır <c>MIN_ACTIVE_ROWVERSION() - 1</c>.
    /// </summary>
    [HttpGet("sync/changes")]
    [Authorize(Policy = Policies.FieldUser)]
    public Task<SyncChangesResponse> Changes([FromQuery] string? since, [FromQuery] int? limit, CancellationToken ct) =>
        sync.ChangesAsync(since, limit, ct);

    /// <summary>Çevrimdışı kuyruktaki işlemleri uygular (en çok 100); her işlem kendi id'siyle idempotent.</summary>
    [HttpPost("sync/push")]
    [Authorize(Policy = Policies.FieldUser)]
    public Task<PushResponse> Push(PushRequest request, CancellationToken ct) => push.PushAsync(request, ct);

    /// <summary>
    /// Sipariş oluşturur (Confirmed). <c>Idempotency-Key</c> başlığı zorunlu: aynı anahtar + aynı gövde aynı
    /// yanıtı döner (<c>Idempotent-Replayed: true</c>); farklı gövde 422; fiyat değiştiyse 409.
    /// </summary>
    [HttpPost("orders")]
    [Authorize(Policy = Policies.FieldSales)]
    [ProducesResponseType<FieldOrderCreated>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateOrder([FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        FieldOrderRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(idempotencyKey, out var key) || key == Guid.Empty)
            throw new ValidationException([new ValidationFailure(IdempotencyKeyHeader, "Idempotency-Key başlığı (UUID) zorunlu.")]);

        var result = await orders.CreateAsync(key, request, ct);
        if (result.Replayed) Response.Headers[ReplayedHeader] = "true";
        return new ContentResult
        {
            StatusCode = result.StatusCode,
            Content = result.Json,
            ContentType = "application/json; charset=utf-8",
        };
    }

    /// <summary>Teslimat kanıtı: <c>photo</c> (JPEG/PNG ≤ 5 MB) ve <c>signature</c> (PNG ≤ 1 MB). Tekrar yükleme üzerine yazar.</summary>
    [HttpPost("deliveries/{id:int}/proof")]
    [Authorize(Policy = Policies.FieldDriver)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxProofRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxProofRequestBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadProof(int id, IFormFile? photo, IFormFile? signature, CancellationToken ct)
    {
        await using var photoStream = photo?.OpenReadStream();
        await using var signatureStream = signature?.OpenReadStream();
        await devices.UploadProofAsync(id,
            photo is null ? null : new ProofFile(photoStream!, photo.ContentType, photo.Length, photo.FileName),
            signature is null ? null : new ProofFile(signatureStream!, signature.ContentType, signature.Length, signature.FileName),
            ct);
        return NoContent();
    }

    /// <summary>Cihaz kaydı (kullanıcı + installationId upsert).</summary>
    [HttpPost("devices")]
    [Authorize(Policy = Policies.FieldUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RegisterDevice(DeviceRequest request, CancellationToken ct)
    {
        await devices.RegisterDeviceAsync(request, ct);
        return NoContent();
    }
}
