namespace ErpAssistant.Application.Abstractions;

/// <summary>Kullanıcısız iç işlemler (tohumlama, worker) için kimlik.</summary>
public sealed class SystemUser : ICurrentUser
{
    public static readonly SystemUser Instance = new();

    public bool IsAuthenticated => true;
    public bool IsSystem => true;
    public string? UserId => "system";
    public string? UserName => "system";
    public IReadOnlyCollection<string> Roles => [];
    public int? SalesRepresentativeId => null;
}
