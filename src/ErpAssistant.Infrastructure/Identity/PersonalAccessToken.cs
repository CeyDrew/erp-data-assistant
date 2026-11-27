namespace ErpAssistant.Infrastructure.Identity;

/// <summary>
/// Ajanların (MCP istemcileri) kullanıcı adına bağlanması için uzun ömürlü erişim token'ı. Yalnızca SHA-256
/// özeti saklanır; düz metin token oluşturulduğu anda bir kez gösterilir.
/// </summary>
public sealed class PersonalAccessToken
{
    public const string Prefix = "erp_pat_";

    public int Id { get; set; }
    public required string UserId { get; set; }
    public required string Name { get; set; }
    public required string TokenHash { get; set; }

    /// <summary>Listede tanımak için ilk karakterler (ör. erp_pat_AbC1).</summary>
    public required string Hint { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;
}
