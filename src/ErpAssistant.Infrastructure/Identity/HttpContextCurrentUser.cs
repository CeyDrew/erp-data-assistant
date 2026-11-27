using System.Security.Claims;
using ErpAssistant.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace ErpAssistant.Infrastructure.Identity;

/// <summary>
/// Kullanıcıyı HTTP isteğinin kimliğinden okur. HTTP bağlamı yoksa (başlangıçta migration,
/// tohumlama) sistem kullanıcısı gibi davranır.
/// </summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsSystem => accessor.HttpContext is null;
    public bool IsAuthenticated => IsSystem || Principal?.Identity?.IsAuthenticated == true;
    public string? UserId => IsSystem ? "system" : Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Principal?.FindFirstValue("sub");
    public string? UserName => IsSystem ? "system" : Principal?.FindFirstValue(ErpClaims.FullName) ?? Principal?.Identity?.Name;

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ErpClaims.Role).Select(c => c.Value).ToArray() ?? [];

    public int? SalesRepresentativeId =>
        int.TryParse(Principal?.FindFirstValue(ErpClaims.SalesRepresentativeId), out var id) ? id : null;

    public int? DriverId =>
        int.TryParse(Principal?.FindFirstValue(ErpClaims.DriverId), out var id) ? id : null;
}
