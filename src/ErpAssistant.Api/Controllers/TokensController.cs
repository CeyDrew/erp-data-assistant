using ErpAssistant.Application.Abstractions;
using ErpAssistant.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ErpAssistant.Api.Controllers;

public sealed record CreateTokenRequest(string Name, int Days = 30);

/// <summary>MCP ve diğer ajan istemcileri için kişisel erişim token'ları.</summary>
public sealed class TokensController(PersonalAccessTokenService tokens, ICurrentUser user) : ApiControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<TokenItem>> List(CancellationToken ct) => tokens.ListAsync(user.UserId!, ct);

    /// <summary>Yeni token oluşturur. Düz metin token yalnızca bu yanıtta döner.</summary>
    [HttpPost]
    public async Task<ActionResult<CreatedToken>> Create(CreateTokenRequest request, CancellationToken ct) =>
        await tokens.CreateAsync(user.UserId!, request.Name, request.Days, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Revoke(int id, CancellationToken ct) =>
        await tokens.RevokeAsync(user.UserId!, id, ct) ? NoContent() : NotFound();
}
