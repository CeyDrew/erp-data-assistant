using ErpAssistant.Domain.Common;

namespace ErpAssistant.Domain.Organization;

/// <summary>Teslimat sürücüsü. Sürücü kullanıcıları yalnızca kendilerine atanmış teslimatları görür.</summary>
public sealed class Driver : Entity
{
    public required string FullName { get; set; }
    public required string Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>Sürücünün çalıştığı şehir; demo planlayıcı teslimatları bu şehre yakın siparişlerden seçer.</summary>
    public required string City { get; set; }
}
