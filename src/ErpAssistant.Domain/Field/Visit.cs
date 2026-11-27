using ErpAssistant.Domain.Common;
using ErpAssistant.Domain.Organization;
using ErpAssistant.Domain.Sales;

namespace ErpAssistant.Domain.Field;

public enum VisitStatus
{
    Planned = 0,
    CheckedIn = 1,
    Completed = 2,
    Skipped = 3,
}

/// <summary>Satış temsilcisinin günlük ziyaret planındaki bir müşteri ziyareti.</summary>
public sealed class Visit : Entity
{
    public const int MaxNoteLength = 1000;

    public int SalesRepresentativeId { get; set; }
    public SalesRepresentative SalesRepresentative { get; set; } = null!;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public DateOnly PlannedDate { get; set; }
    public int Sequence { get; set; }
    public VisitStatus Status { get; set; }
    public DateTime? CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }
    public double? CheckInLatitude { get; set; }
    public double? CheckInLongitude { get; set; }
    public string? Note { get; set; }

    public static Visit Plan(int salesRepresentativeId, int customerId, DateOnly plannedDate, int sequence)
    {
        if (sequence <= 0)
            throw new DomainException("Ziyaret sırası pozitif olmalı.");

        return new Visit
        {
            SalesRepresentativeId = salesRepresentativeId,
            CustomerId = customerId,
            PlannedDate = plannedDate,
            Sequence = sequence,
            Status = VisitStatus.Planned,
        };
    }

    public void CheckIn(DateTime at, double? latitude, double? longitude)
    {
        if (Status != VisitStatus.Planned)
            throw new DomainException("Yalnızca planlanmış ziyarete giriş yapılabilir.");
        Geo.Validate(latitude, longitude);

        Status = VisitStatus.CheckedIn;
        CheckInAt = at;
        CheckInLatitude = latitude;
        CheckInLongitude = longitude;
    }

    public void CheckOut(DateTime at, string? note)
    {
        if (Status != VisitStatus.CheckedIn)
            throw new DomainException("Çıkış için önce ziyarete giriş yapılmalı.");
        if (at < CheckInAt)
            throw new DomainException("Çıkış zamanı giriş zamanından önce olamaz.");
        if (note?.Length > MaxNoteLength)
            throw new DomainException($"Not en fazla {MaxNoteLength} karakter olabilir.");

        Status = VisitStatus.Completed;
        CheckOutAt = at;
        if (!string.IsNullOrWhiteSpace(note)) Note = note.Trim();
    }

    public void Skip(string? note)
    {
        if (Status != VisitStatus.Planned)
            throw new DomainException("Yalnızca planlanmış ziyaret atlanabilir.");

        Status = VisitStatus.Skipped;
        if (!string.IsNullOrWhiteSpace(note)) Note = note.Trim();
    }
}

internal static class Geo
{
    public static void Validate(double? latitude, double? longitude)
    {
        if ((latitude is null) != (longitude is null))
            throw new DomainException("Konum için enlem ve boylam birlikte gönderilmeli.");
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw new DomainException("Geçersiz konum.");
    }
}
