using ErpAssistant.Application.Abstractions;
using ErpAssistant.Application.Field;
using ErpAssistant.Domain.Ai;
using ErpAssistant.Domain.Billing;
using ErpAssistant.Domain.Catalog;
using ErpAssistant.Domain.Documents;
using ErpAssistant.Domain.Field;
using ErpAssistant.Domain.Inventory;
using ErpAssistant.Domain.Organization;
using ErpAssistant.Domain.Sales;
using ErpAssistant.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ErpAssistant.Infrastructure.Persistence;

public class ErpDbContext(DbContextOptions<ErpDbContext> options, ICurrentUser currentUser)
    : IdentityDbContext<AppUser>(options), IErpDbContext
{
    /// <summary>Mobil sipariş numaraları (SM-yyyy-NNNNNN) için sequence.</summary>
    public const string OrderNumberSequence = "FieldOrderNumbers";

    /// <summary>Saha sevkiyatında kesilen fatura numaraları (FS-yyyy-NNNNNN) için sequence.</summary>
    public const string InvoiceNumberSequence = "FieldInvoiceNumbers";

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<SalesRepresentative> SalesRepresentatives => Set<SalesRepresentative>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<AiQueryLog> AiQueryLogs => Set<AiQueryLog>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<LocationPing> LocationPings => Set<LocationPing>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<FieldTombstone> FieldTombstones => Set<FieldTombstone>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PersonalAccessToken> PersonalAccessTokens => Set<PersonalAccessToken>();

    // Global query filter'lar bu özellikleri okur. EF Core, DbContext üyelerine yapılan başvuruları
    // her sorguda parametre olarak yeniden değerlendirir; model bir kez derlenir, değer her istekte değişir.
    internal int? RepFilter => AccessPolicy.RowFilterFor(currentUser);
    internal int? DriverFilter => AccessPolicy.DriverFilterFor(currentUser);
    internal IReadOnlyCollection<string>? DepartmentFilter => AccessPolicy.DepartmentsFor(currentUser);
    internal bool DepartmentFilterOff => DepartmentFilter is null;
    internal string[] AllowedDepartments => DepartmentFilter?.ToArray() ?? [];

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Türkçe büyük/küçük harf duyarsız arama: "istanbul" = "İSTANBUL", "ışık" = "IŞIK".
        modelBuilder.UseCollation("Turkish_CI_AS");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ErpDbContext).Assembly);
        modelBuilder.HasSequence<long>(OrderNumberSequence).StartsAt(1).IncrementsBy(1);
        modelBuilder.HasSequence<long>(InvoiceNumberSequence).StartsAt(1).IncrementsBy(1);

        // Satır bazlı yetki. Satış temsilcisi yalnızca kendi müşterilerini, siparişlerini, faturalarını
        // ve tahsilatlarını görür. AI ve MCP araçları da bu bağlamı kullandığından filtre atlanamaz.
        modelBuilder.Entity<Customer>().HasQueryFilter(c => RepFilter == null || c.SalesRepresentativeId == RepFilter);
        modelBuilder.Entity<Order>().HasQueryFilter(o => RepFilter == null || o.SalesRepresentativeId == RepFilter);
        modelBuilder.Entity<OrderLine>().HasQueryFilter(l => RepFilter == null || l.Order.SalesRepresentativeId == RepFilter);
        modelBuilder.Entity<Invoice>().HasQueryFilter(i => RepFilter == null || i.SalesRepresentativeId == RepFilter);
        modelBuilder.Entity<Payment>().HasQueryFilter(p => RepFilter == null || p.Invoice.SalesRepresentativeId == RepFilter);

        // Saha modülü. Ziyaret: temsilcinin kendi planı. Teslimat: sürücü yalnızca kendine atanmışları,
        // temsilci kendi siparişlerininkileri (denormalize SalesRepresentativeId), yönetici/depo hepsini görür.
        modelBuilder.Entity<Visit>().HasQueryFilter(v => RepFilter == null || v.SalesRepresentativeId == RepFilter);
        modelBuilder.Entity<Delivery>().HasQueryFilter(d =>
            (DriverFilter == null && RepFilter == null) || d.DriverId == DriverFilter || d.SalesRepresentativeId == RepFilter);

        // Doküman görünürlüğü departmana göre (vektör aramasında da aynı liste kullanılır).
        modelBuilder.Entity<Document>().HasQueryFilter(d => DepartmentFilterOff || AllowedDepartments.Contains(d.Department.Code));
        modelBuilder.Entity<DocumentChunk>().HasQueryFilter(c => DepartmentFilterOff || AllowedDepartments.Contains(c.Document.Department.Code));

        ConfigureRowVersions(modelBuilder);
    }

    /// <summary>
    /// Delta senkron için rowversion gölge kolonu. ulong'a big-endian çevrilir; sıralama korunur, sorguda
    /// <c>Version &gt; @cursor</c> doğrudan ikili karşılaştırmaya dönüşür. Eşzamanlılık belirteci yalnızca
    /// mobil durum makinelerinde (ziyaret, teslimat) açıktır; diğer tablolarda mevcut yazma davranışı değişmez.
    /// </summary>
    private static void ConfigureRowVersions(ModelBuilder modelBuilder)
    {
        Type[] versioned =
        [
            typeof(Customer), typeof(Product), typeof(Order), typeof(StockMovement), typeof(Invoice), typeof(Payment),
            typeof(Visit), typeof(Delivery), typeof(FieldTombstone),
        ];
        foreach (var type in versioned)
        {
            var property = modelBuilder.Entity(type).Property<ulong>(FieldVersion.Property)
                .IsRowVersion()
                .HasConversion(new NumberToBytesConverter<ulong>())
                .HasColumnType("rowversion");
            if (type != typeof(Visit) && type != typeof(Delivery))
                property.IsConcurrencyToken(false);
            modelBuilder.Entity(type).HasIndex(FieldVersion.Property);
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<string>().HaveMaxLength(256);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AddFieldTombstones();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AddFieldTombstones();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Mobil istemcilerin yerel kopyadan silmesi gereken değişiklikleri tombstone olarak aynı işlemde yazar:
    /// müşteri başka temsilciye geçti, ürün pasifleşti, ziyaret/teslimat silindi ya da yeniden atandı.
    /// Satır yeni sahibine kendi rowversion'ı değiştiği için zaten gider; tombstone eski sahibi içindir.
    /// </summary>
    private void AddFieldTombstones()
    {
        if (ChangeTracker.AutoDetectChangesEnabled) ChangeTracker.DetectChanges();

        var now = DateTime.UtcNow;
        var tombstones = new List<FieldTombstone>();
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Modified or EntityState.Deleted)) continue;
            var deleted = entry.State == EntityState.Deleted;

            switch (entry.Entity)
            {
                case Customer c when deleted || Changed(entry, nameof(Customer.SalesRepresentativeId)):
                    tombstones.Add(Tombstone(FieldTombstone.Customer, c.Id, rep: Original<int>(entry, nameof(Customer.SalesRepresentativeId))));
                    break;
                case Product p when deleted || (Changed(entry, nameof(Product.IsActive)) && !p.IsActive):
                    tombstones.Add(Tombstone(FieldTombstone.Product, p.Id));
                    break;
                case Visit v when deleted || Changed(entry, nameof(Visit.SalesRepresentativeId)):
                    tombstones.Add(Tombstone(FieldTombstone.Visit, v.Id, rep: Original<int>(entry, nameof(Visit.SalesRepresentativeId))));
                    break;
                case Delivery d when deleted || Changed(entry, nameof(Delivery.DriverId)):
                    tombstones.Add(Tombstone(FieldTombstone.Delivery, d.Id, driver: Original<int>(entry, nameof(Delivery.DriverId))));
                    break;
            }
        }
        if (tombstones.Count > 0) FieldTombstones.AddRange(tombstones);

        FieldTombstone Tombstone(string entity, int id, int? rep = null, int? driver = null) => new()
        {
            Entity = entity,
            EntityId = id,
            SalesRepresentativeId = rep,
            DriverId = driver,
            CreatedAt = now,
        };
    }

    private static bool Changed(EntityEntry entry, string property)
    {
        var p = entry.Property(property);
        return p.IsModified && !Equals(p.OriginalValue, p.CurrentValue);
    }

    private static T Original<T>(EntityEntry entry, string property) => (T)entry.Property(property).OriginalValue!;
}
