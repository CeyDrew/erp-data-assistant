using System.Net;
using System.Net.Http.Json;
using ErpAssistant.Application.Auth;
using ErpAssistant.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace ErpAssistant.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AuthTests(ErpApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Wrong_password_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(DemoUsers.Admin, "yanlis-sifre"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_returns_roles_and_financial_visibility()
    {
        var auth = await _client.LoginAsync(DemoUsers.Warehouse);

        auth.User.Roles.Should().Equal("DepoSorumlusu");
        auth.User.CanSeeFinancials.Should().BeFalse();
        auth.User.DocumentDepartments.Should().BeEquivalentTo(["DEPO", "KALITE"]);
    }

    [Fact]
    public async Task Refresh_rotates_and_reuse_revokes_the_whole_family()
    {
        var login = await _client.LoginAsync(DemoUsers.Admin);

        var first = await Refresh(login.RefreshToken);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = (await first.Content.ReadFromJsonAsync<AuthResponse>(HttpExtensions.Json))!;
        rotated.RefreshToken.Should().NotBe(login.RefreshToken);

        // Eski token tekrar kullanıldı: reddedilir ve yeni token da iptal olur.
        (await Refresh(login.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Refresh(rotated.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        var login = await _client.LoginAsync(DemoUsers.SalesRep);

        (await _client.PostAsJsonAsync("/api/v1/auth/logout", new RefreshRequest(login.RefreshToken)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Refresh(login.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private Task<HttpResponseMessage> Refresh(string token) =>
        _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(token));
}
