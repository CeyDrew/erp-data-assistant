using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ErpAssistant.Infrastructure.Persistence;

/// <summary>`dotnet ef migrations add` için; çalışma zamanında kullanılmaz.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ErpDbContext>
{
    public ErpDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Erp")
            ?? "Server=localhost,1433;Database=ErpAssistant;User Id=sa;Password=Erp_Asistan_2026!;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new ErpDbContext(options);
    }
}
