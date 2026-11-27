using ErpAssistant.Domain.Common;
using ErpAssistant.Domain.Organization;

namespace ErpAssistant.Domain.Sales;

public sealed class Customer : Entity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string TaxNumber { get; set; }
    public required string City { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>Açık adres (mobil ziyaret ve teslimat için).</summary>
    public string? Address { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public decimal CreditLimit { get; set; }
    public int SalesRepresentativeId { get; set; }
    public SalesRepresentative SalesRepresentative { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
