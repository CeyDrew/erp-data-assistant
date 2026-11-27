using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErpAssistant.Infrastructure.Identity;

/// <summary><c>Authorization: Bearer erp_pat_...</c> başlığını doğrulayan kimlik doğrulama şeması.</summary>
public sealed class PersonalAccessTokenHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    PersonalAccessTokenService tokens)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "PersonalAccessToken";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();

        var principal = await tokens.AuthenticateAsync(header["Bearer ".Length..].Trim(), Scheme.Name, Context.RequestAborted);
        return principal is null
            ? AuthenticateResult.Fail("Geçersiz, süresi dolmuş veya iptal edilmiş erişim token'ı.")
            : AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
