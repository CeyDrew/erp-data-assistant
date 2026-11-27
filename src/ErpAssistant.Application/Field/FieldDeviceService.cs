using ErpAssistant.Application.Abstractions;
using ErpAssistant.Application.Common;
using ErpAssistant.Application.Documents;
using ErpAssistant.Domain.Field;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpAssistant.Application.Field;

/// <summary>Teslimat kanıtı (fotoğraf + imza) yükleme ve cihaz kaydı.</summary>
public sealed class FieldDeviceService(
    IErpDbContext db,
    IFieldDatabase fieldDb,
    ICurrentUser user,
    IFileStorage storage,
    TimeProvider clock,
    ILogger<FieldDeviceService> logger)
{
    public const long MaxPhotoBytes = 5 * 1024 * 1024;
    public const long MaxSignatureBytes = 1024 * 1024;

    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];

    public async Task UploadProofAsync(int deliveryId, ProofFile? photo, ProofFile? signature, CancellationToken ct)
    {
        // Önce yetki: başkasının teslimatı için dosya içeriği hiç okunmaz.
        var delivery = await FieldPushService.LoadDeliveryForDriverAsync(db, user, deliveryId, ct);

        var failures = new List<ValidationFailure>();
        var photoBytes = await ReadAsync(photo, "photo", MaxPhotoBytes, ["image/jpeg", "image/png"], failures, ct);
        var signatureBytes = await ReadAsync(signature, "signature", MaxSignatureBytes, ["image/png"], failures, ct);
        if (failures.Count > 0) throw new ValidationException(failures);

        var photoExt = IsPng(photoBytes!) ? ".png" : ".jpg";
        var photoPath = await storage.SaveAsync(new MemoryStream(photoBytes!), $"proof-{deliveryId}{photoExt}", ct);
        var signaturePath = await storage.SaveAsync(new MemoryStream(signatureBytes!), $"signature-{deliveryId}.png", ct);

        var previous = new[] { delivery.ProofPhotoPath, delivery.SignaturePath };
        try
        {
            delivery.AttachProof(photoPath, signaturePath);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            fieldDb.DiscardChanges();
            await DeleteQuietlyAsync([photoPath, signaturePath]);
            throw;
        }

        // Tekrar yükleme üzerine yazar: eski dosyalar kayıt güncellendikten sonra silinir.
        await DeleteQuietlyAsync(previous);
    }

    public async Task RegisterDeviceAsync(DeviceRequest request, CancellationToken ct)
    {
        var userId = user.UserId ?? throw new ForbiddenException("Kullanıcı kimliği yok.");
        var now = clock.GetUtcNow().UtcDateTime;
        var installationId = request.InstallationId.Trim();

        var device = await db.Devices.FirstOrDefaultAsync(d => d.UserId == userId && d.InstallationId == installationId, ct);
        if (device is null)
        {
            device = new Device
            {
                UserId = userId,
                InstallationId = installationId,
                Platform = request.Platform,
                AppVersion = request.AppVersion.Trim(),
                CreatedAt = now,
            };
            db.Devices.Add(device);
        }

        device.Platform = request.Platform;
        device.AppVersion = request.AppVersion.Trim();
        device.PushToken = string.IsNullOrWhiteSpace(request.PushToken) ? null : request.PushToken.Trim();
        device.LastSeenAt = now;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (fieldDb.IsUniqueViolation(ex))
        {
            // Aynı kurulum eşzamanlı kaydedildi; son yazan kazanır, ikinci deneme güncelleme olur.
            fieldDb.DiscardChanges();
            await RegisterDeviceAsync(request, ct);
        }
    }

    private static async Task<byte[]?> ReadAsync(ProofFile? file, string field, long maxBytes, string[] contentTypes,
        List<ValidationFailure> failures, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            failures.Add(new ValidationFailure(field, "Dosya zorunlu."));
            return null;
        }
        if (file.Length > maxBytes)
        {
            failures.Add(new ValidationFailure(field, $"Dosya en fazla {maxBytes / 1024 / 1024} MB olabilir."));
            return null;
        }
        if (file.ContentType is null || !contentTypes.Contains(file.ContentType.Split(';')[0].Trim(), StringComparer.OrdinalIgnoreCase))
        {
            failures.Add(new ValidationFailure(field, $"İzin verilen türler: {string.Join(", ", contentTypes)}."));
            return null;
        }

        using var buffer = new MemoryStream();
        await file.Content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        if (bytes.Length > maxBytes)
        {
            failures.Add(new ValidationFailure(field, $"Dosya en fazla {maxBytes / 1024 / 1024} MB olabilir."));
            return null;
        }

        // Başlıktaki içerik türüne güvenilmez; dosyanın ilk baytları beyan edilen türle eşleşmeli.
        var declared = file.ContentType.Split(';')[0].Trim().ToLowerInvariant();
        var matches = declared == "image/png" ? IsPng(bytes) : IsJpeg(bytes);
        if (!matches)
        {
            failures.Add(new ValidationFailure(field, "Dosya içeriği beyan edilen görüntü türüyle uyuşmuyor."));
            return null;
        }
        return bytes;
    }

    private static bool IsPng(byte[] bytes) => bytes.AsSpan().StartsWith(PngMagic);
    private static bool IsJpeg(byte[] bytes) => bytes.AsSpan().StartsWith(JpegMagic);

    private async Task DeleteQuietlyAsync(IEnumerable<string?> paths)
    {
        foreach (var path in paths.OfType<string>())
        {
            try
            {
                await storage.DeleteAsync(path, CancellationToken.None);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                logger.LogWarning(ex, "Eski teslimat kanıtı silinemedi: {Path}", path);
            }
        }
    }
}
