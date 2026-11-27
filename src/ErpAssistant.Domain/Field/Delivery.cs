using ErpAssistant.Domain.Common;
using ErpAssistant.Domain.Organization;
using ErpAssistant.Domain.Sales;

namespace ErpAssistant.Domain.Field;

public enum DeliveryStatus
{
    Assigned = 0,
    Delivered = 1,
    Failed = 2,
}

public enum DeliveryFailureReason
{
    CustomerAbsent = 1,
    AddressNotFound = 2,
    Refused = 3,
    Damaged = 4,
    Other = 5,
}

/// <summary>Sevk edilmiş bir siparişin sürücüye atanmış teslimatı.</summary>
public sealed class Delivery : Entity
{
    public const int MaxNoteLength = 1000;
    public const int MaxRecipientLength = 128;

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int DriverId { get; set; }
    public Driver Driver { get; set; } = null!;

    /// <summary>
    /// Siparişin temsilcisi (denormalize). Temsilcinin kendi siparişlerinin teslimatlarını görmesi için
    /// satır filtresi join'siz çalışır.
    /// </summary>
    public int SalesRepresentativeId { get; set; }

    public DateOnly PlannedDate { get; set; }
    public int Sequence { get; set; }
    public DeliveryStatus Status { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DeliveryFailureReason? FailureReason { get; set; }
    public string? RecipientName { get; set; }
    public string? Note { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ProofPhotoPath { get; set; }
    public string? SignaturePath { get; set; }

    public bool HasProof => ProofPhotoPath is not null && SignaturePath is not null;

    public static Delivery Assign(Order order, int driverId, DateOnly plannedDate, int sequence)
    {
        if (order.Status != OrderStatus.Shipped)
            throw new DomainException("Yalnızca sevk edilmiş sipariş teslimata atanabilir.");
        if (sequence <= 0)
            throw new DomainException("Teslimat sırası pozitif olmalı.");

        return new Delivery
        {
            OrderId = order.Id,
            Order = order,
            DriverId = driverId,
            SalesRepresentativeId = order.SalesRepresentativeId,
            PlannedDate = plannedDate,
            Sequence = sequence,
            Status = DeliveryStatus.Assigned,
        };
    }

    /// <summary>Teslimatı tamamlar ve siparişi teslim edildi olarak işaretler.</summary>
    public void Complete(Order order, DateTime at, string recipientName, string? note, double? latitude, double? longitude)
    {
        if (order.Id != OrderId)
            throw new DomainException("Teslimat ile sipariş eşleşmiyor.");
        if (Status != DeliveryStatus.Assigned)
            throw new DomainException("Yalnızca atanmış teslimat tamamlanabilir.");
        if (string.IsNullOrWhiteSpace(recipientName))
            throw new DomainException("Teslim alan kişinin adı zorunlu.");
        if (recipientName.Trim().Length > MaxRecipientLength)
            throw new DomainException($"Teslim alan adı en fazla {MaxRecipientLength} karakter olabilir.");
        Geo.Validate(latitude, longitude);
        ValidateNote(note);

        order.MarkDelivered();
        Status = DeliveryStatus.Delivered;
        DeliveredAt = at;
        RecipientName = recipientName.Trim();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        Latitude = latitude;
        Longitude = longitude;
    }

    public void Fail(DateTime at, DeliveryFailureReason reason, string? note)
    {
        if (Status != DeliveryStatus.Assigned)
            throw new DomainException("Yalnızca atanmış teslimat başarısız olarak işaretlenebilir.");
        if (!Enum.IsDefined(reason))
            throw new DomainException("Geçersiz başarısızlık nedeni.");
        if (reason == DeliveryFailureReason.Other && string.IsNullOrWhiteSpace(note))
            throw new DomainException("\"Diğer\" nedeni için açıklama zorunlu.");
        ValidateNote(note);

        Status = DeliveryStatus.Failed;
        DeliveredAt = at;
        FailureReason = reason;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    /// <summary>Teslimat fotoğrafı ve imza; tekrar yüklemede eskisinin yerini alır.</summary>
    public void AttachProof(string photoPath, string signaturePath)
    {
        if (Status == DeliveryStatus.Failed)
            throw new DomainException("Başarısız teslimata kanıt eklenemez.");

        ProofPhotoPath = photoPath;
        SignaturePath = signaturePath;
    }

    private static void ValidateNote(string? note)
    {
        if (note?.Length > MaxNoteLength)
            throw new DomainException($"Not en fazla {MaxNoteLength} karakter olabilir.");
    }
}
