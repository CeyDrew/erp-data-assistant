using Microsoft.AspNetCore.Identity;

namespace ErpAssistant.Infrastructure.Identity;

public sealed class AppUser : IdentityUser
{
    public required string FullName { get; set; }
    public int? SalesRepresentativeId { get; set; }

    /// <summary>Kullanıcı bir teslimat sürücüsüyse sürücü kimliği (JWT claim: driver_id).</summary>
    public int? DriverId { get; set; }
}
