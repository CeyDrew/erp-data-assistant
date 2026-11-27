using FluentValidation;

namespace ErpAssistant.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed record RefreshRequest(string RefreshToken);

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(200);
}

public sealed record AuthResponse(
    string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, DateTime RefreshTokenExpiresAt, UserInfo User);

public sealed record UserInfo(
    string Id, string Email, string FullName, IReadOnlyList<string> Roles, int? SalesRepresentativeId, bool CanSeeFinancials,
    IReadOnlyCollection<string>? DocumentDepartments);
