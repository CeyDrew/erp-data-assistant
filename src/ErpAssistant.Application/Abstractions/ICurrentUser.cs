namespace ErpAssistant.Application.Abstractions;

/// <summary>İsteği yapan kullanıcı. REST'te JWT'den, MCP'de erişim token'ından, worker'da sistemden gelir.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Migration, tohumlama ve worker gibi kullanıcısız iç işlemler; satır filtresi uygulanmaz.</summary>
    bool IsSystem { get; }

    string? UserId { get; }
    string? UserName { get; }
    IReadOnlyCollection<string> Roles { get; }

    /// <summary>Kullanıcı bir satış temsilcisine bağlıysa temsilci kimliği.</summary>
    int? SalesRepresentativeId { get; }

    /// <summary>Fiyat, maliyet, ciro ve tahsilat görebilir mi? (Depo sorumlusu göremez.)</summary>
    bool CanSeeFinancials => AccessPolicy.CanSeeFinancials(this);

    /// <summary>Doküman aramasında erişilebilir departman kodları; null = hepsi.</summary>
    IReadOnlyCollection<string>? DepartmentCodes => AccessPolicy.DepartmentsFor(this);
}
