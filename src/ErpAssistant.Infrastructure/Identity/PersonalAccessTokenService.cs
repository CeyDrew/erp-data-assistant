using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ErpAssistant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ErpAssistant.Infrastructure.Identity;

public sealed record CreatedToken(int Id, string Name, string Token, DateTime ExpiresAt);

public sealed record TokenItem(int Id, string Name, string Hint, DateTime CreatedAt, DateTime ExpiresAt, DateTime? LastUsedAt, bool Active);

public sealed class PersonalAccessTokenService(ErpDbContext db, UserManager<AppUser> users, TimeProvider clock)
{
    public const int MaxActivePerUser = 10;

    public async Task<CreatedToken> CreateAsync(string userId, string name, int days, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 64)
            throw new FluentValidation.ValidationException("Token adı 1-64 karakter olmalı.");
        if (days is < 1 or > 365)
            throw new FluentValidation.ValidationException("Geçerlilik 1-365 gün olmalı.");

        var now = clock.GetUtcNow().UtcDateTime;
        var active = await db.PersonalAccessTokens.CountAsync(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now, ct);
        if (active >= MaxActivePerUser)
            throw new FluentValidation.ValidationException($"En fazla {MaxActivePerUser} etkin token olabilir.");

        var token = PersonalAccessToken.Prefix + Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var entity = new PersonalAccessToken
        {
            UserId = userId,
            Name = name.Trim(),
            TokenHash = Hash(token),
            Hint = token[..(PersonalAccessToken.Prefix.Length + 4)],
            CreatedAt = now,
            ExpiresAt = now.AddDays(days),
        };
        db.PersonalAccessTokens.Add(entity);
        await db.SaveChangesAsync(ct);
        return new CreatedToken(entity.Id, entity.Name, token, entity.ExpiresAt);
    }

    public async Task<IReadOnlyList<TokenItem>> ListAsync(string userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return await db.PersonalAccessTokens.AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Id)
            .Select(t => new TokenItem(t.Id, t.Name, t.Hint, t.CreatedAt, t.ExpiresAt, t.LastUsedAt, t.RevokedAt == null && t.ExpiresAt > now))
            .ToListAsync(ct);
    }

    public async Task<bool> RevokeAsync(string userId, int id, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return await db.PersonalAccessTokens
            .Where(t => t.Id == id && t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct) > 0;
    }

    /// <summary>Token'ı doğrular ve JWT ile aynı claim'leri taşıyan kimliği üretir.</summary>
    public async Task<ClaimsPrincipal?> AuthenticateAsync(string token, string scheme, CancellationToken ct)
    {
        if (!token.StartsWith(PersonalAccessToken.Prefix, StringComparison.Ordinal)) return null;

        var now = clock.GetUtcNow().UtcDateTime;
        var hash = Hash(token);
        var entity = await db.PersonalAccessTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (entity is null || !entity.IsActive(now)) return null;

        var user = await users.FindByIdAsync(entity.UserId);
        if (user is null) return null;

        // Son kullanım zamanını dakikada bir güncelle (her istekte yazma yapmamak için).
        if (entity.LastUsedAt is null || now - entity.LastUsedAt > TimeSpan.FromMinutes(1))
        {
            entity.LastUsedAt = now;
            await db.SaveChangesAsync(ct);
        }

        var claims = new List<Claim>
        {
            new("sub", user.Id),
            new("email", user.Email!),
            new(ErpClaims.FullName, user.FullName),
            new("token_id", entity.Id.ToString()),
        };
        claims.AddRange((await users.GetRolesAsync(user)).Select(r => new Claim(ErpClaims.Role, r)));
        if (user.SalesRepresentativeId is { } rep)
            claims.Add(new Claim(ErpClaims.SalesRepresentativeId, rep.ToString()));
        if (user.DriverId is { } driver)
            claims.Add(new Claim(ErpClaims.DriverId, driver.ToString()));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, scheme, "email", ErpClaims.Role));
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
