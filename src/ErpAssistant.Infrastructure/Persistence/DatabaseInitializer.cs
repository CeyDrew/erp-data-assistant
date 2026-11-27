using ErpAssistant.Infrastructure.Identity;
using ErpAssistant.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ErpAssistant.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>Migration'ları uygular; boşsa sentetik veriyi ve demo kullanıcılarını yükler.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));

        logger.LogInformation("Migration'lar uygulanıyor");
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));
        await db.Database.MigrateAsync(ct);

        await scope.ServiceProvider.GetRequiredService<ErpDataSeeder>().SeedAsync(ct);
        await scope.ServiceProvider.GetRequiredService<FieldDataSeeder>().SeedAsync(ct);
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync(ct);
    }
}
