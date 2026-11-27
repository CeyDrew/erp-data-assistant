namespace ErpAssistant.Application.Abstractions;

/// <summary>İsteği yapan kullanıcı. REST'te JWT'den, MCP'de erişim token'ından, worker'da sistemden gelir.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    string? UserId { get; }
    string? UserName { get; }
    IReadOnlyCollection<string> Roles { get; }

    /// <summary>Satış temsilcisi ise kendi temsilci kimliği; satır filtresi bununla çalışır.</summary>
    int? SalesRepresentativeId { get; }

    /// <summary>Doküman aramasında erişilebilir departman kodları; null = hepsi.</summary>
    IReadOnlyCollection<string>? DepartmentCodes { get; }

    /// <summary>Fiyat, maliyet, ciro ve tahsilat görebilir mi? (Depo sorumlusu göremez.)</summary>
    bool CanSeeFinancials { get; }
}
