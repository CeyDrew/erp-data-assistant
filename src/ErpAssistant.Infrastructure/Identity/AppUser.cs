using Microsoft.AspNetCore.Identity;

namespace ErpAssistant.Infrastructure.Identity;

public sealed class AppUser : IdentityUser
{
    public required string FullName { get; set; }
    public int? SalesRepresentativeId { get; set; }
}
