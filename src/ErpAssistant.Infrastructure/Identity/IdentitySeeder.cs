using ErpAssistant.Application.Abstractions;
using ErpAssistant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpAssistant.Infrastructure.Identity;

/// <summary>Demo kullanıcıları: her rolden biri ve her satış temsilcisi için bir hesap.</summary>
public sealed class IdentitySeeder(
    ErpDbContext db,
    UserManager<AppUser> users,
    RoleManager<IdentityRole> roles,
    ILogger<IdentitySeeder> logger)
{
    public const string DemoPassword = "Demo123!";

    public async Task SeedAsync(CancellationToken ct)
    {
        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));
        }

        await EnsureUserAsync("yonetici@erp.local", "Deniz Yönetici", Roles.Admin, null);
        await EnsureUserAsync("depo@erp.local", "Kerem Depocu", Roles.Warehouse, null);

        var reps = await db.SalesRepresentatives.AsNoTracking().OrderBy(r => r.Id).ToListAsync(ct);
        foreach (var rep in reps)
            await EnsureUserAsync(rep.Email, rep.FullName, Roles.SalesRep, rep.Id);

        var drivers = await db.Drivers.AsNoTracking().OrderBy(d => d.Id).ToListAsync(ct);
        foreach (var driver in drivers)
            await EnsureUserAsync(driver.Email, driver.FullName, Roles.Driver, null, driver.Id);
    }

    private async Task EnsureUserAsync(string email, string fullName, string role, int? repId, int? driverId = null)
    {
        if (await users.FindByEmailAsync(email) is not null) return;

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            SalesRepresentativeId = repId,
            DriverId = driverId,
        };
        var result = await users.CreateAsync(user, DemoPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        await users.AddToRoleAsync(user, role);
        logger.LogInformation("Demo kullanıcısı oluşturuldu: {Email} ({Role})", email, role);
    }
}
