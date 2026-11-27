using ErpAssistant.Application.Abstractions;

namespace ErpAssistant.Api.Infrastructure;

/// <summary>Kimlik doğrulama eklenene kadar geçici: tam yetkili kullanıcı.</summary>
internal sealed class AnonymousCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => false;
    public string? UserId => null;
    public string? UserName => "anonim";
    public IReadOnlyCollection<string> Roles => [];
    public int? SalesRepresentativeId => null;
    public IReadOnlyCollection<string>? DepartmentCodes => null;
    public bool CanSeeFinancials => true;
}
