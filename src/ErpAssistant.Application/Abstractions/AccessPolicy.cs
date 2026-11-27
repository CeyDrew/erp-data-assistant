using ErpAssistant.Domain.Organization;

namespace ErpAssistant.Application.Abstractions;

public static class Roles
{
    public const string Admin = "Yonetici";
    public const string SalesRep = "SatisTemsilcisi";
    public const string Warehouse = "DepoSorumlusu";
    public const string Driver = "Surucu";

    public static readonly IReadOnlyList<string> All = [Admin, SalesRep, Warehouse, Driver];
}

/// <summary>
/// Rol → görünürlük kuralları tek yerde. Global query filter'lar, servisler, AI araçları ve vektör
/// araması buradan okur; kural iki yerde tutulmaz.
/// </summary>
public static class AccessPolicy
{
    /// <summary>Hiçbir satırla eşleşmeyen temsilci kimliği: filtre "kapalı" konumda başarısız olur.</summary>
    public const int NoAccess = -1;

    /// <summary>
    /// Müşteri, sipariş, fatura ve tahsilat satırları için temsilci filtresi.
    /// null = filtre yok; aksi halde yalnızca bu temsilcinin satırları. Sürücü bu satırları REST'te
    /// görmez (NoAccess); teslimat için gereken müşteri bilgisi teslimat nesnesinin içinde gelir.
    /// </summary>
    public static int? RowFilterFor(ICurrentUser user)
    {
        if (user.IsSystem) return null;
        if (!user.IsAuthenticated) return NoAccess;
        if (user.Roles.Contains(Roles.Admin)) return null;
        if (user.Roles.Contains(Roles.SalesRep)) return user.SalesRepresentativeId ?? NoAccess;
        if (user.Roles.Contains(Roles.Warehouse)) return null; // sevkiyat için tüm siparişler; fiyatlar ayrıca gizlenir
        return NoAccess;
    }

    /// <summary>
    /// Teslimat satırları için sürücü filtresi. null = filtre yok (yönetici, depo, sistem); aksi halde
    /// yalnızca bu sürücüye atanmış teslimatlar. Satış temsilcisi ayrıca <see cref="RowFilterFor"/> ile
    /// kendi siparişlerinin teslimatlarını görür (bkz. ErpDbContext).
    /// </summary>
    public static int? DriverFilterFor(ICurrentUser user)
    {
        if (user.IsSystem) return null;
        if (!user.IsAuthenticated) return NoAccess;
        if (user.Roles.Contains(Roles.Admin)) return null;
        if (user.Roles.Contains(Roles.Driver)) return user.DriverId ?? NoAccess;
        if (user.Roles.Contains(Roles.SalesRep)) return NoAccess;
        if (user.Roles.Contains(Roles.Warehouse)) return null;
        return NoAccess;
    }

    public static bool CanSeeFinancials(ICurrentUser user) =>
        user.IsSystem || user.Roles.Contains(Roles.Admin) || user.Roles.Contains(Roles.SalesRep);

    public static IReadOnlyCollection<string>? DepartmentsFor(ICurrentUser user)
    {
        if (user.IsSystem || user.Roles.Contains(Roles.Admin)) return null;

        var departments = new HashSet<string>();
        if (user.Roles.Contains(Roles.SalesRep))
            departments.UnionWith([DepartmentCodes.Sales, DepartmentCodes.Quality]);
        if (user.Roles.Contains(Roles.Warehouse))
            departments.UnionWith([DepartmentCodes.Warehouse, DepartmentCodes.Quality]);
        return departments;
    }
}
