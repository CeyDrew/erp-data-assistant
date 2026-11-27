namespace ErpAssistant.Infrastructure.Identity;

/// <summary>
/// Dönen (rotating) yenileme token'ı. Veritabanında yalnızca SHA-256 özeti tutulur. Aynı aileden
/// iptal edilmiş bir token tekrar kullanılırsa token çalınmış sayılır ve tüm aile iptal edilir.
/// </summary>
public sealed class RefreshToken
{
    public long Id { get; set; }
    public required string UserId { get; set; }
    public required string TokenHash { get; set; }
    public Guid FamilyId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByHash { get; set; }

    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;
}
