using ErpAssistant.Application.Abstractions;
using ErpAssistant.Application.Auth;
using ErpAssistant.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ErpAssistant.Api.Controllers;

public sealed class AuthController(TokenService tokens, UserManager<AppUser> users, ICurrentUser currentUser)
    : ApiControllerBase
{
    /// <summary>E-posta ve şifreyle giriş; kısa ömürlü access token ve dönen refresh token verir.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        try
        {
            return await tokens.LoginAsync(request, ct);
        }
        catch (AuthenticationFailedException ex)
        {
            return Problem(title: "Giriş başarısız", detail: ex.Message, statusCode: StatusCodes.Status401Unauthorized);
        }
    }

    /// <summary>Refresh token'ı yenisiyle değiştirir (rotasyon). Kullanılmış token tekrar gelirse aile iptal edilir.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken ct)
    {
        try
        {
            return await tokens.RefreshAsync(request.RefreshToken, ct);
        }
        catch (AuthenticationFailedException ex)
        {
            return Problem(title: "Oturum yenilenemedi", detail: ex.Message, statusCode: StatusCodes.Status401Unauthorized);
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await tokens.RevokeAsync(request.RefreshToken, ct);
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserInfo>> Me()
    {
        var user = await users.FindByIdAsync(currentUser.UserId!);
        return user is null ? Unauthorized() : await tokens.DescribeAsync(user);
    }
}
