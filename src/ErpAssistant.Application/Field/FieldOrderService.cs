using ErpAssistant.Application.Abstractions;
using ErpAssistant.Application.Common;
using ErpAssistant.Domain.Common;
using ErpAssistant.Domain.Field;
using ErpAssistant.Domain.Sales;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ErpAssistant.Application.Field;

/// <summary>
/// Mobil sipariş oluşturma. Çevrimdışı istemci aynı isteği birden çok kez gönderebilir; sonuç
/// <see cref="IdempotencyRecord"/> tablosunda anahtarla saklanır ve tekrar gelen istek aynı yanıtı alır.
/// </summary>
public sealed class FieldOrderService(
    IErpDbContext db,
    IFieldDatabase fieldDb,
    ICurrentUser user,
    IValidator<FieldOrderRequest> validator,
    TimeProvider clock)
{
    public const string Operation = PushTypes.OrderCreate;

    public async Task<IdempotentResponse> CreateAsync(Guid key, FieldOrderRequest request, CancellationToken ct)
    {
        if (key == Guid.Empty)
            throw new ValidationException([new ValidationFailure("Idempotency-Key", "Idempotency-Key geçerli bir UUID olmalı.")]);
        await validator.ValidateAndThrowAsync(request, ct);

        var userId = user.UserId ?? throw new ForbiddenException("Kullanıcı kimliği yok.");
        var hash = FieldJson.Hash(request);

        if (await FindStoredAsync(key, userId, hash, ct) is { } stored)
            return stored;

        try
        {
            return await fieldDb.ExecuteInTransactionAsync(async c =>
            {
                fieldDb.DiscardChanges();
                var (order, shortLines) = await BuildAsync(key, request, c);
                db.Orders.Add(order);
                await db.SaveChangesAsync(c);

                // Yanıt sipariş kimliğini içerdiğinden kayıt siparişten sonra, aynı işlem içinde yazılır.
                var json = FieldJson.Serialize(new FieldOrderCreated(FieldMapper.ToSync(order), shortLines));
                db.IdempotencyRecords.Add(new IdempotencyRecord
                {
                    Key = key,
                    UserId = userId,
                    Operation = Operation,
                    RequestHash = hash,
                    StatusCode = 201,
                    ResponseJson = json,
                    CreatedAt = clock.GetUtcNow().UtcDateTime,
                });
                await db.SaveChangesAsync(c);
                return new IdempotentResponse(201, json, false);
            }, ct);
        }
        catch (DbUpdateException ex) when (fieldDb.IsUniqueViolation(ex))
        {
            // Aynı anahtarla eşzamanlı iki istek: ikisi de kontrolü geçti, biri önce commit etti. Bizim
            // INSERT'imiz (Orders.ClientRequestId ya da IdempotencyRecords.Key benzersiz index'i) kazananın
            // commit'ini bekleyip ihlalle döndü; kazananın kaydı artık okunabilir.
            fieldDb.DiscardChanges();
            if (await FindStoredAsync(key, userId, hash, ct) is { } winner)
                return winner;
            throw;
        }
    }

    private async Task<IdempotentResponse?> FindStoredAsync(Guid key, string userId, string hash, CancellationToken ct)
    {
        var record = await db.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Key == key, ct);
        if (record is null) return null;
        if (record.UserId != userId || record.Operation != Operation || record.RequestHash != hash)
            throw new IdempotencyKeyReusedException();
        return new IdempotentResponse(record.StatusCode, record.ResponseJson, true);
    }

    private async Task<(Order Order, List<ShortLine> ShortLines)> BuildAsync(Guid key, FieldOrderRequest request, CancellationToken ct)
    {
        // Müşteri satır filtresinden geçer: temsilci başka temsilcinin müşterisine sipariş açamaz (404).
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct)
            ?? throw new NotFoundException("Müşteri", request.CustomerId);
        if (!await db.Warehouses.AnyAsync(w => w.Id == request.WarehouseId, ct))
            throw new NotFoundException("Depo", request.WarehouseId);

        var productIds = request.Lines.Select(l => l.ProductId).ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        foreach (var line in request.Lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
                throw new NotFoundException("Ürün", line.ProductId);
            if (!product.IsActive)
                throw new DomainException($"{product.Sku} pasif; sipariş verilemez.");
        }

        // Fiyat yeniden doğrulanır: istemcinin önbelleği eskiyse sipariş kaydedilmez, güncel fiyatlar döner.
        var changes = request.Lines
            .Where(l => Math.Round(l.UnitPrice, 2) != products[l.ProductId].UnitPrice)
            .Select(l => new PriceChange(l.ProductId, l.UnitPrice, products[l.ProductId].UnitPrice))
            .ToList();
        if (changes.Count > 0)
        {
            var newTotal = request.Lines.Sum(l =>
                OrderLine.CalculateTotal(l.Quantity, products[l.ProductId].UnitPrice, l.DiscountRate));
            throw new ConflictException(PushCodes.PriceChanged, "Bazı ürünlerin fiyatı değişti; sipariş kaydedilmedi.",
                new Dictionary<string, object?> { ["lines"] = changes, ["newTotal"] = newTotal });
        }

        var available = await db.StockBalances.AsNoTracking()
            .Where(b => b.WarehouseId == request.WarehouseId && productIds.Contains(b.ProductId))
            .ToDictionaryAsync(b => b.ProductId, b => b.Quantity, ct);

        var now = clock.GetUtcNow().UtcDateTime;
        var sequence = await fieldDb.NextOrderSequenceAsync(ct);
        var order = Order.Create($"SM-{now:yyyy}-{sequence:D6}", customer, request.WarehouseId, now);
        order.ClientRequestId = key;
        order.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        // Stok yetmeyen kalem düşürülmez: eksik miktarıyla kaydedilir, depo tamamlar ya da parçalı sevk eder.
        var shortLines = new List<ShortLine>();
        foreach (var line in request.Lines)
        {
            var orderLine = order.AddLine(products[line.ProductId], line.Quantity, line.DiscountRate);
            var stock = Math.Max(0, available.GetValueOrDefault(line.ProductId));
            if (line.Quantity > stock)
            {
                orderLine.FlagShortage(line.Quantity - stock);
                shortLines.Add(new ShortLine(line.ProductId, line.Quantity, stock));
            }
        }
        order.Confirm();
        return (order, shortLines);
    }
}
