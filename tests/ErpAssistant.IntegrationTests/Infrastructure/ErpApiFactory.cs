using ErpAssistant.Infrastructure.Ai;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace ErpAssistant.IntegrationTests.Infrastructure;

/// <summary>
/// Gerçek SQL Server konteyneri üzerinde API'yi bellekte ayağa kaldırır. Küçük bir sentetik veri
/// setiyle migration + tohumlama bir kez yapılır; tüm testler aynı veritabanını okur.
/// </summary>
public sealed class ErpApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private string _connectionString = "";

    /// <summary>Yönetici olarak oturum açmış istemci.</summary>
    public HttpClient Admin { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        _connectionString = new SqlConnectionStringBuilder(_sql.GetConnectionString())
        {
            InitialCatalog = "ErpAssistantTests",
            TrustServerCertificate = true,
        }.ConnectionString;

        // Host'u şimdi kurarak migration ve tohumlamanın testlerden önce bitmesini sağlıyoruz.
        _ = Services;
        Admin = await this.CreateClientAsAsync(DemoUsers.Admin);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Erp", _connectionString);
        builder.UseSetting("Database:InitializeOnStartup", "true");
        builder.UseSetting("Seed:Customers", "40");
        builder.UseSetting("Seed:Products", "60");
        builder.UseSetting("Seed:Orders", "800");
        builder.UseSetting("Seed:SalesRepresentatives", "4");
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");
        builder.UseSetting("RateLimiting:AuthPerMinute", "10000");
        builder.UseSetting("RateLimiting:ChatPerMinute", "10000");
        builder.ConfigureTestServices(s => s.AddSingleton<IChatClientFactory, ScriptedChatClientFactory>());
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _sql.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ErpApiFactory>
{
    public const string Name = "api";
}
