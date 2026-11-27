using ErpAssistant.Application.Abstractions;
using ErpAssistant.Infrastructure.Persistence;
using ErpAssistant.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ErpAssistant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Erp")
            ?? throw new InvalidOperationException("ConnectionStrings:Erp tanımlı değil.");

        services.AddDbContext<ErpDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.EnableRetryOnFailure(5)));
        services.AddScoped<IErpDbContext>(sp => sp.GetRequiredService<ErpDbContext>());

        services.AddSingleton(TimeProvider.System);
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.AddScoped<ErpDataSeeder>();

        return services;
    }
}
