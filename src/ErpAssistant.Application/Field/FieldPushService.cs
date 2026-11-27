using System.Text.Json;
using ErpAssistant.Application.Abstractions;
using ErpAssistant.Application.Common;
using ErpAssistant.Domain.Common;
using ErpAssistant.Domain.Field;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpAssistant.Application.Field;

/// <summary>
/// Çevrimdışı kuyruğun sunucuya boşaltılması. İşlemler sırayla ve her biri kendi kaydıyla uygulanır;
/// biri başarısız olursa diğerleri etkilenmez. Her işlem <c>id</c>'siyle idempotenttir: uygulanmış
/// işlem tekrar gelirse <c>duplicate</c> ve ilk sonucu döner.
/// </summary>
public sealed class FieldPushService(
    IErpDbContext db,
    IFieldDatabase fieldDb,
    ICurrentUser user,
    FieldOrderService orders,
    TimeProvider clock,
    ILogger<FieldPushService> logger)
{
    public const int MaxOperations = 100;
    public const int MaxPointsPerBatch = 500;

    public async Task<PushResponse> PushAsync(PushRequest request, CancellationToken ct)
    {
        var results = new List<PushResult>(request.Operations.Count);
        foreach (var operation in request.Operations)
            results.Add(await ApplyAsync(operation, ct));
        return new PushResponse(results);
    }

    private async Task<PushResult> ApplyAsync(PushOperation op, CancellationToken ct)
    {
        try
        {
            if (op.Type == PushTypes.OrderCreate)
            {
                var order = await orders.CreateAsync(op.Id, FieldJson.Payload<FieldOrderRequest>(op.Payload), ct);
                return new PushResult(op.Id, order.Replayed ? PushStatus.Duplicate : PushStatus.Applied, null, null,
                    FieldJson.Parse(order.Json));
            }

            var userId = user.UserId ?? throw new ForbiddenException("Kullanıcı kimliği yok.");
            var hash = FieldJson.Sha256(op.Type + "|" + op.Payload.GetRawText());
            if (await FindStoredAsync(op, userId, hash, ct) is { } duplicate)
                return duplicate;

            object data = op.Type switch
            {
                PushTypes.VisitCheckIn => await CheckInAsync(FieldJson.Payload<VisitCheckInPayload>(op.Payload), ct),
                PushTypes.VisitCheckOut => await CheckOutAsync(FieldJson.Payload<VisitCheckOutPayload>(op.Payload), ct),
                PushTypes.DeliveryComplete => await CompleteAsync(FieldJson.Payload<DeliveryCompletePayload>(op.Payload), ct),
                PushTypes.DeliveryFail => await FailAsync(FieldJson.Payload<DeliveryFailPayload>(op.Payload), ct),
                PushTypes.LocationBatch => await LocationsAsync(userId, FieldJson.Payload<LocationBatchPayload>(op.Payload), ct),
                _ => throw Invalid("type", $"Bilinmeyen işlem türü: {op.Type}"),
            };

            // Değişiklik ve idempotency kaydı tek SaveChanges'ta (tek işlemde) yazılır.
            var json = FieldJson.Serialize(data);
            db.IdempotencyRecords.Add(new IdempotencyRecord
            {
                Key = op.Id,
                UserId = userId,
                Operation = op.Type,
                RequestHash = hash,
                StatusCode = 200,
                ResponseJson = json,
                CreatedAt = clock.GetUtcNow().UtcDateTime,
            });
            await db.SaveChangesAsync(ct);
            return new PushResult(op.Id, PushStatus.Applied, null, null, FieldJson.Parse(json));
        }
        catch (ConflictException ex)
        {
            fieldDb.DiscardChanges();
            return new PushResult(op.Id, PushStatus.Conflict, ex.Code, ex.Message, FieldJson.ToElement(ex.Extensions));
        }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException || fieldDb.IsUniqueViolation(ex))
        {
            // Aynı işlem eşzamanlı başka bir istekte uygulandı.
            fieldDb.DiscardChanges();
            var stored = await db.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Key == op.Id, ct);
            return stored is not null
                ? new PushResult(op.Id, PushStatus.Duplicate, null, null, FieldJson.Parse(stored.ResponseJson))
                : new PushResult(op.Id, PushStatus.Conflict, null, "Kayıt eşzamanlı olarak değişti; yeniden senkronlayın.", null);
        }
        catch (Exception ex) when (Classify(ex) is { } code)
        {
            fieldDb.DiscardChanges();
            logger.LogInformation("Push işlemi reddedildi: {Type} {Id} {Code}: {Message}", op.Type, op.Id, code, ex.Message);
            return new PushResult(op.Id, PushStatus.Rejected, code, ex.Message, null);
        }
    }

    private static string? Classify(Exception ex) => ex switch
    {
        NotFoundException => PushCodes.NotFound,
        ForbiddenException => PushCodes.Forbidden,
        ValidationException or IdempotencyKeyReusedException => PushCodes.Validation,
        DomainException => PushCodes.Domain,
        _ => null,
    };

    private async Task<PushResult?> FindStoredAsync(PushOperation op, string userId, string hash, CancellationToken ct)
    {
        var record = await db.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Key == op.Id, ct);
        if (record is null) return null;
        if (record.UserId != userId || record.Operation != op.Type || record.RequestHash != hash)
            return new PushResult(op.Id, PushStatus.Rejected, PushCodes.Validation, new IdempotencyKeyReusedException().Message, null);
        return new PushResult(op.Id, PushStatus.Duplicate, null, null, FieldJson.Parse(record.ResponseJson));
    }

    // --- Ziyaret ---------------------------------------------------------------------------------

    private async Task<VisitSync> CheckInAsync(VisitCheckInPayload p, CancellationToken ct)
    {
        var visit = await LoadVisitAsync(p.VisitId, ct);
        visit.CheckIn(At(p.At), p.Latitude, p.Longitude);
        return FieldMapper.ToSync(visit);
    }

    private async Task<VisitSync> CheckOutAsync(VisitCheckOutPayload p, CancellationToken ct)
    {
        var visit = await LoadVisitAsync(p.VisitId, ct);
        visit.CheckOut(At(p.At), p.Note);
        return FieldMapper.ToSync(visit);
    }

    private async Task<Visit> LoadVisitAsync(int visitId, CancellationToken ct)
    {
        if (!user.Roles.Contains(Roles.SalesRep) && !user.Roles.Contains(Roles.Admin))
            throw new ForbiddenException("Ziyaret işlemleri yalnızca satış temsilcisine açık.");
        // Ziyaret filtresi: temsilci yalnızca kendi ziyaretlerini görür; başkasınınki 404.
        return await db.Visits.FirstOrDefaultAsync(v => v.Id == visitId, ct)
            ?? throw new NotFoundException("Ziyaret", visitId);
    }

    // --- Teslimat --------------------------------------------------------------------------------

    private async Task<object> CompleteAsync(DeliveryCompletePayload p, CancellationToken ct)
    {
        var delivery = await LoadDeliveryForDriverAsync(db, user, p.DeliveryId, ct);
        // Sürücü siparişleri göremez; sipariş yalnızca filtrelenmiş teslimatın OrderId'siyle okunur.
        var order = await db.Orders.IgnoreQueryFilters().FirstAsync(o => o.Id == delivery.OrderId, ct);
        delivery.Complete(order, At(p.At), p.RecipientName ?? "", p.Note, p.Latitude, p.Longitude);
        return new { deliveryId = delivery.Id, status = delivery.Status, orderStatus = order.Status };
    }

    private async Task<object> FailAsync(DeliveryFailPayload p, CancellationToken ct)
    {
        if (p.Reason is not { } reason)
            throw Invalid("reason", "Başarısızlık nedeni zorunlu.");
        var delivery = await LoadDeliveryForDriverAsync(db, user, p.DeliveryId, ct);
        delivery.Fail(At(p.At), reason, p.Note);
        return new { deliveryId = delivery.Id, status = delivery.Status, failureReason = delivery.FailureReason };
    }

    /// <summary>
    /// Teslimat filtresi temsilciye kendi siparişlerinin teslimatlarını da gösterir; değişikliği ise yalnızca
    /// teslimatın sürücüsü (ya da yönetici) yapabilir.
    /// </summary>
    internal static async Task<Delivery> LoadDeliveryForDriverAsync(IErpDbContext db, ICurrentUser user, int deliveryId,
        CancellationToken ct)
    {
        var delivery = await db.Deliveries.FirstOrDefaultAsync(d => d.Id == deliveryId, ct)
            ?? throw new NotFoundException("Teslimat", deliveryId);
        var isDriver = user.Roles.Contains(Roles.Driver) && user.DriverId == delivery.DriverId;
        if (!isDriver && !user.Roles.Contains(Roles.Admin))
            throw new ForbiddenException("Bu teslimat size atanmamış.");
        return delivery;
    }

    // --- Konum -----------------------------------------------------------------------------------

    private async Task<object> LocationsAsync(string userId, LocationBatchPayload p, CancellationToken ct)
    {
        var points = p.Points ?? [];
        if (points.Count > MaxPointsPerBatch)
            throw Invalid("points", $"Bir pakette en fazla {MaxPointsPerBatch} nokta olabilir.");
        foreach (var point in points)
        {
            if (point.Id == Guid.Empty || point.At == default)
                throw Invalid("points", "Her noktada id ve at zorunlu.");
            if (point.Latitude is < -90 or > 90 || point.Longitude is < -180 or > 180 || point.Accuracy < 0)
                throw Invalid("points", "Geçersiz konum.");
        }

        // Nokta kimliği istemcide üretilir; daha önce (başka bir paketle) gelmiş noktalar atlanır.
        var ids = points.Select(x => x.Id).Distinct().ToList();
        var existing = (await db.LocationPings.AsNoTracking()
            .Where(x => ids.Contains(x.ClientId)).Select(x => x.ClientId).ToListAsync(ct)).ToHashSet();

        var accepted = 0;
        foreach (var point in points.DistinctBy(x => x.Id).Where(x => !existing.Contains(x.Id)))
        {
            db.LocationPings.Add(new LocationPing
            {
                UserId = userId,
                ClientId = point.Id,
                RecordedAt = point.At.UtcDateTime,
                Latitude = point.Latitude,
                Longitude = point.Longitude,
                Accuracy = point.Accuracy,
            });
            accepted++;
        }
        return new { accepted, duplicates = points.Count - accepted };
    }

    private static DateTime At(DateTimeOffset at) =>
        at == default ? throw Invalid("at", "İşlem zamanı (at) zorunlu.") : at.UtcDateTime;

    private static ValidationException Invalid(string property, string message) =>
        new([new ValidationFailure(property, message)]);
}
