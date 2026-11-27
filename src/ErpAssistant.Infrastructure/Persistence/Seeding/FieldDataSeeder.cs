using Bogus;
using ErpAssistant.Application.Field;
using ErpAssistant.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpAssistant.Infrastructure.Persistence.Seeding;

/// <summary>
/// Saha modülü verisi: demo sürücüler, müşteri adres/koordinatları ve ürün barkodları. Her açılışta çalışır
/// ve idempotenttir; saha modülünden önce tohumlanmış (mevcut Docker volume'u) veritabanları da eksik
/// alanları burada tamamlar. Değerler kayıt kimliğinden türetildiği için her kurulumda aynıdır.
/// </summary>
public sealed class FieldDataSeeder(ErpDbContext db, ILogger<FieldDataSeeder> logger)
{
    /// <summary>Koordinat sapması (derece): ≈ ±3 km, müşteriler il merkezinin çevresine dağılır.</summary>
    private const double Jitter = 0.03;

    private static readonly (string Email, string FullName, string Phone, string City)[] DriverSeeds =
    [
        ("surucu@erp.local", "Murat Yıldırım", "0532 410 10 01", "İstanbul"),
        ("surucu2@erp.local", "Ayşe Kaya", "0533 410 10 02", "Ankara"),
        ("surucu3@erp.local", "Emre Şahin", "0534 410 10 03", "İzmir"),
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await EnsureDriversAsync(ct);
        var customers = await BackfillCustomersAsync(ct);
        var products = await BackfillBarcodesAsync(ct);
        if (customers + products > 0)
            logger.LogInformation("Saha verisi tamamlandı: {Customers} müşteri konumu, {Products} barkod", customers, products);
    }

    private async Task EnsureDriversAsync(CancellationToken ct)
    {
        var existing = await db.Drivers.Select(d => d.Email).ToListAsync(ct);
        foreach (var seed in DriverSeeds.Where(s => !existing.Contains(s.Email)))
            db.Drivers.Add(new Driver { Email = seed.Email, FullName = seed.FullName, Phone = seed.Phone, City = seed.City });
        await db.SaveChangesAsync(ct);
    }

    private async Task<int> BackfillCustomersAsync(CancellationToken ct)
    {
        var customers = await db.Customers.Where(c => c.Latitude == null || c.Longitude == null || c.Address == null)
            .OrderBy(c => c.Id).ToListAsync(ct);
        foreach (var customer in customers)
        {
            var faker = new Faker("tr") { Random = new Randomizer(customer.Id * 7_919) };
            var (lat, lng) = TurkishCities.CenterOf(customer.City);
            var street = string.Join(' ', faker.Address.StreetName().Split(' ', StringSplitOptions.RemoveEmptyEntries));
            customer.Address ??= $"{street} No:{faker.Random.Int(1, 180)}, {customer.City}";
            customer.Latitude = Math.Round(lat + faker.Random.Double(-Jitter, Jitter), 6);
            customer.Longitude = Math.Round(lng + faker.Random.Double(-Jitter, Jitter), 6);
        }
        await db.SaveChangesAsync(ct);
        return customers.Count;
    }

    private async Task<int> BackfillBarcodesAsync(CancellationToken ct)
    {
        var products = await db.Products.Where(p => p.Barcode == null).ToListAsync(ct);
        foreach (var product in products)
            product.Barcode = Ean13.ForProduct(product.Id);
        await db.SaveChangesAsync(ct);
        return products.Count;
    }
}
