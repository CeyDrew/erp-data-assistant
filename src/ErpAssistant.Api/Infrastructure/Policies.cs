using ErpAssistant.Application.Abstractions;

namespace ErpAssistant.Api.Infrastructure;

public static class Policies
{
    /// <summary>Fiyat, ciro, fatura ve tahsilat verisi.</summary>
    public const string Financials = "financials";

    public static void Configure(Microsoft.AspNetCore.Authorization.AuthorizationOptions o)
    {
        o.AddPolicy(Financials, p => p.RequireRole(Roles.Admin, Roles.SalesRep));
    }
}
