using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ErpAssistant.Application.Abstractions;
using ErpAssistant.Application.Auth;
using ErpAssistant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ErpAssistant.Infrastructure.Identity;

public sealed class AuthenticationFailedException(string message) : Exception(message);

public sealed class TokenService(
    ErpDbContext db,
    UserManager<AppUser> users,
    IOptions<JwtOptions> options,
    TimeProvider clock,
    ILogger<TokenService> logger)
{
    private JwtOptions Options => options.Value;

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null || !await users.CheckPasswordAsync(user, request.Password))
            throw new AuthenticationFailedException("E-posta veya şifre hatalı.");

        return await IssueAsync(user, Guid.NewGuid(), ct);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var hash = Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
            ?? throw new AuthenticationFailedException("Geçersiz yenileme token'ı.");

        if (stored.RevokedAt is not null)
        {
            // Döndürülmüş (kullanılmış) bir token tekrar geldi: çalınmış olabilir. Aileyi kapat.
            logger.LogWarning("Yenileme token'ı yeniden kullanıldı; aile iptal ediliyor. Kullanıcı {UserId}", stored.UserId);
            await db.RefreshTokens
                .Where(t => t.FamilyId == stored.FamilyId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
            throw new AuthenticationFailedException("Yenileme token'ı daha önce kullanılmış.");
        }

        if (stored.ExpiresAt <= now)
            throw new AuthenticationFailedException("Yenileme token'ının süresi dolmuş.");

        var user = await users.FindByIdAsync(stored.UserId)
            ?? throw new AuthenticationFailedException("Kullanıcı bulunamadı.");

        var response = await IssueAsync(user, stored.FamilyId, ct, rotated: stored);
        return response;
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null) return;

        await db.RefreshTokens
            .Where(t => t.FamilyId == stored.FamilyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    public async Task<UserInfo> DescribeAsync(AppUser user)
    {
        var roles = (await users.GetRolesAsync(user)).ToList();
        ICurrentUser principal = new ClaimsCurrentUser(roles, user.SalesRepresentativeId);
        return new UserInfo(user.Id, user.Email!, user.FullName, roles, user.SalesRepresentativeId,
            principal.CanSeeFinancials, principal.DepartmentCodes);
    }

    private async Task<AuthResponse> IssueAsync(AppUser user, Guid familyId, CancellationToken ct, RefreshToken? rotated = null)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var info = await DescribeAsync(user);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ErpClaims.FullName, user.FullName),
        };
        claims.AddRange(info.Roles.Select(r => new Claim(ErpClaims.Role, r)));
        if (user.SalesRepresentativeId is { } repId)
            claims.Add(new Claim(ErpClaims.SalesRepresentativeId, repId.ToString()));

        var accessExpires = now.AddMinutes(Options.AccessTokenMinutes);
        var accessToken = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Options.Issuer,
            Audience = Options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = accessExpires,
            SigningCredentials = new SigningCredentials(SigningKey(Options), SecurityAlgorithms.HmacSha256),
        });

        var refreshToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48));
        var refreshHash = Hash(refreshToken);
        var refreshExpires = now.AddDays(Options.RefreshTokenDays);

        if (rotated is not null)
        {
            rotated.RevokedAt = now;
            rotated.ReplacedByHash = refreshHash;
        }

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            FamilyId = familyId,
            CreatedAt = now,
            ExpiresAt = refreshExpires,
        });
        await db.SaveChangesAsync(ct);

        return new AuthResponse(accessToken, accessExpires, refreshToken, refreshExpires, info);
    }

    public static SymmetricSecurityKey SigningKey(JwtOptions options)
    {
        var bytes = Encoding.UTF8.GetBytes(options.SigningKey);
        if (bytes.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey en az 32 bayt olmalı.");
        return new SymmetricSecurityKey(bytes);
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private sealed class ClaimsCurrentUser(IReadOnlyCollection<string> roles, int? repId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public bool IsSystem => false;
        public string? UserId => null;
        public string? UserName => null;
        public IReadOnlyCollection<string> Roles => roles;
        public int? SalesRepresentativeId => repId;
    }
}
