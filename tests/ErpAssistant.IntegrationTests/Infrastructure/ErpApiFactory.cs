using ErpAssistant.Infrastructure.Ai;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using ErpAssistant.Infrastructure.Documents;
using RabbitMQ.Client;
using Testcontainers.MsSql;
using Testcontainers.Qdrant;
using Testcontainers.RabbitMq;

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

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder().WithImage("rabbitmq:4-management").Build();
    private readonly QdrantContainer _qdrant = new QdrantBuilder().WithImage("qdrant/qdrant:v1.15.4").Build();
    private readonly string _storage = Path.Combine(Path.GetTempPath(), "erp-tests-" + Guid.NewGuid().ToString("N"));

    private string _connectionString = "";

    /// <summary>Yönetici olarak oturum açmış istemci.</summary>
    public HttpClient Admin { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_sql.StartAsync(), _rabbit.StartAsync(), _qdrant.StartAsync());
        _connectionString = new SqlConnectionStringBuilder(_sql.GetConnectionString())
        {
            InitialCatalog = "ErpAssistantTests",
            TrustServerCertificate = true,
        }.ConnectionString;

        // Host'u şimdi kurarak migration ve tohumlamanın testlerden önce bitmesini sağlıyoruz.
        _ = Services;
        Admin = await this.CreateClientAsAsync(DemoUsers.Admin);
    }

    public async Task<uint> DeadLetterCountAsync()
    {
        var factory = new ConnectionFactory { Uri = new Uri(_rabbit.GetConnectionString()) };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        return (await channel.QueueDeclarePassiveAsync("documents.dead")).MessageCount;
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
        builder.UseSetting("RabbitMq:Uri", _rabbit.GetConnectionString());
        builder.UseSetting("RabbitMq:RetryDelayMs", "300");
        builder.UseSetting("Qdrant:Host", _qdrant.Hostname);
        builder.UseSetting("Qdrant:Port", _qdrant.GetMappedPublicPort(6334).ToString());
        builder.UseSetting("Qdrant:MinScore", "0.2");
        builder.UseSetting("Storage:Path", _storage);
        builder.UseSetting("SampleDocuments:Path", "");
        builder.ConfigureTestServices(s =>
        {
            s.AddSingleton<IChatClientFactory, ScriptedChatClientFactory>();
            s.AddSingleton<IEmbeddingGeneratorFactory, HashingEmbeddingFactory>();
            // Worker süreci yerine tüketici aynı test host'unda çalışır.
            s.AddDocumentProcessing();
        });
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await Task.WhenAll(_sql.DisposeAsync().AsTask(), _rabbit.DisposeAsync().AsTask(), _qdrant.DisposeAsync().AsTask());
        try { Directory.Delete(_storage, true); } catch (IOException) { }
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ErpApiFactory>
{
    public const string Name = "api";
}
