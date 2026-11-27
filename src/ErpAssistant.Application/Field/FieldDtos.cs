using System.Text.Json;
using ErpAssistant.Domain.Field;
using ErpAssistant.Domain.Sales;
using FluentValidation;

namespace ErpAssistant.Application.Field;

// --- GET /field/sync/changes -----------------------------------------------------------------

public sealed record SyncChangesResponse(
    string Cursor,
    bool HasMore,
    DateTime ServerTime,
    IReadOnlyList<WarehouseSync> Warehouses,
    IReadOnlyList<CustomerSync> Customers,
    IReadOnlyList<ProductSync> Products,
    IReadOnlyList<StockSync> Stock,
    IReadOnlyList<OrderSync> Orders,
    IReadOnlyList<VisitSync> Visits,
    IReadOnlyList<DeliverySync> Deliveries,
    IReadOnlyList<TombstoneSync> Tombstones);

public sealed record WarehouseSync(int Id, string Code, string Name, string City);

public sealed record CustomerSync(
    int Id, string Code, string Name, string City, string? Address, string? Phone, string? Email,
    double? Latitude, double? Longitude, decimal CreditLimit, decimal Balance);

public sealed record ProductSync(
    int Id, string Sku, string? Barcode, string Name, string Category, string Unit, decimal UnitPrice, bool IsActive);

public sealed record StockSync(int ProductId, int WarehouseId, int Quantity);

public sealed record OrderSync(
    int Id, string OrderNumber, Guid? ClientRequestId, int CustomerId, int WarehouseId, DateTime OrderDate,
    OrderStatus Status, decimal TotalAmount, IReadOnlyList<OrderLineSync> Lines);

public sealed record OrderLineSync(
    int ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal DiscountRate, decimal LineTotal,
    int ShortQuantity);

public sealed record VisitSync(
    int Id, int CustomerId, DateOnly PlannedDate, int Sequence, VisitStatus Status, DateTime? CheckInAt,
    DateTime? CheckOutAt, string? Note);

public sealed record DeliverySync(
    int Id, int OrderId, string OrderNumber, DateOnly PlannedDate, int Sequence, DeliveryStatus Status,
    string CustomerName, string? Address, string City, string? Phone, double? Latitude, double? Longitude,
    IReadOnlyList<DeliveryItemSync> Items, DateTime? DeliveredAt, DeliveryFailureReason? FailureReason,
    string? RecipientName, string? Note, bool HasProof);

public sealed record DeliveryItemSync(string ProductName, int Quantity, string Unit);

public sealed record TombstoneSync(string Entity, int Id);

// --- POST /field/orders ----------------------------------------------------------------------

public sealed record FieldOrderRequest(int CustomerId, int WarehouseId, string? Note, IReadOnlyList<FieldOrderLineRequest> Lines);

public sealed record FieldOrderLineRequest(int ProductId, int Quantity, decimal UnitPrice, decimal DiscountRate);

public sealed class FieldOrderRequestValidator : AbstractValidator<FieldOrderRequest>
{
    public const int MaxLines = 100;
    public const int MaxQuantity = 100_000;

    public FieldOrderRequestValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.Lines).NotEmpty().Must(l => l is null || l.Count <= MaxLines)
            .WithMessage($"En fazla {MaxLines} kalem gönderilebilir.");
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => x.ProductId).Distinct().Count() == l.Count)
            .WithMessage("Aynı ürün birden fazla kalemde olamaz.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).InclusiveBetween(1, MaxQuantity);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0);
            line.RuleFor(l => l.DiscountRate).InclusiveBetween(0, 0.5m);
        });
    }
}

public sealed record FieldOrderCreated(OrderSync Order, IReadOnlyList<ShortLine> ShortLines);

public sealed record ShortLine(int ProductId, int Requested, int Available);

public sealed record PriceChange(int ProductId, decimal ClientUnitPrice, decimal UnitPrice);

/// <summary>Saklanan ya da yeni üretilmiş yanıt; <see cref="Replayed"/> ise işlem yeniden çalıştırılmadı.</summary>
public sealed record IdempotentResponse(int StatusCode, string Json, bool Replayed);

// --- POST /field/sync/push -------------------------------------------------------------------

public sealed record PushRequest(IReadOnlyList<PushOperation> Operations);

public sealed record PushOperation(Guid Id, string Type, JsonElement Payload);

public sealed class PushRequestValidator : AbstractValidator<PushRequest>
{
    public PushRequestValidator()
    {
        RuleFor(x => x.Operations).NotNull().Must(o => o is null || o.Count <= FieldPushService.MaxOperations)
            .WithMessage($"En fazla {FieldPushService.MaxOperations} işlem gönderilebilir.");
        RuleForEach(x => x.Operations).ChildRules(op =>
        {
            op.RuleFor(o => o.Id).NotEmpty();
            op.RuleFor(o => o.Type).NotEmpty().MaximumLength(32);
        });
    }
}

public sealed record PushResponse(IReadOnlyList<PushResult> Results);

public sealed record PushResult(Guid Id, string Status, string? Code, string? Message, JsonElement? Data);

public static class PushStatus
{
    public const string Applied = "applied";
    public const string Duplicate = "duplicate";
    public const string Rejected = "rejected";
    public const string Conflict = "conflict";
}

public static class PushCodes
{
    public const string PriceChanged = "price_changed";
    public const string NotFound = "not_found";
    public const string Forbidden = "forbidden";
    public const string Validation = "validation";
    public const string Domain = "domain";
}

public static class PushTypes
{
    public const string OrderCreate = "order.create";
    public const string VisitCheckIn = "visit.checkIn";
    public const string VisitCheckOut = "visit.checkOut";
    public const string DeliveryComplete = "delivery.complete";
    public const string DeliveryFail = "delivery.fail";
    public const string LocationBatch = "location.batch";
}

public sealed record VisitCheckInPayload(int VisitId, DateTimeOffset At, double? Latitude, double? Longitude);

public sealed record VisitCheckOutPayload(int VisitId, DateTimeOffset At, string? Note);

public sealed record DeliveryCompletePayload(
    int DeliveryId, DateTimeOffset At, string? RecipientName, string? Note, double? Latitude, double? Longitude);

public sealed record DeliveryFailPayload(int DeliveryId, DateTimeOffset At, DeliveryFailureReason? Reason, string? Note);

public sealed record LocationBatchPayload(IReadOnlyList<LocationPointPayload>? Points);

public sealed record LocationPointPayload(Guid Id, DateTimeOffset At, double Latitude, double Longitude, double? Accuracy);

// --- POST /field/devices ---------------------------------------------------------------------

public sealed record DeviceRequest(string InstallationId, string Platform, string AppVersion, string? PushToken);

public sealed class DeviceRequestValidator : AbstractValidator<DeviceRequest>
{
    public DeviceRequestValidator()
    {
        RuleFor(x => x.InstallationId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Platform).NotEmpty().Must(p => p is "android" or "ios")
            .WithMessage("Platform 'android' ya da 'ios' olmalı.");
        RuleFor(x => x.AppVersion).NotEmpty().MaximumLength(32);
        RuleFor(x => x.PushToken).MaximumLength(512);
    }
}

// --- POST /field/deliveries/{id}/proof -------------------------------------------------------

public sealed record ProofFile(Stream Content, string? ContentType, long Length, string? FileName);
