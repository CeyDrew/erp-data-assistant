using ErpAssistant.Application.Abstractions;

namespace ErpAssistant.Api.Infrastructure;

public static class Policies
{
    /// <summary>Fiyat, ciro, fatura ve tahsilat verisi.</summary>
    public const string Financials = "financials";

    /// <summary>Mobil satış: sipariş, müşteri, ziyaret.</summary>
    public const string FieldSales = "field-sales";

    /// <summary>Mobil teslimat: teslimat kanıtı.</summary>
    public const string FieldDriver = "field-driver";

    /// <summary>Herhangi bir mobil saha kullanıcısı (senkron, push, cihaz kaydı).</summary>
    public const string FieldUser = "field-user";

    public static void Configure(Microsoft.AspNetCore.Authorization.AuthorizationOptions o)
    {
        o.AddPolicy(Financials, p => p.RequireRole(Roles.Admin, Roles.SalesRep));
        o.AddPolicy(FieldSales, p => p.RequireRole(Roles.SalesRep, Roles.Admin));
        o.AddPolicy(FieldDriver, p => p.RequireRole(Roles.Driver, Roles.Admin));
        o.AddPolicy(FieldUser, p => p.RequireRole(Roles.SalesRep, Roles.Driver, Roles.Admin));
    }
}
