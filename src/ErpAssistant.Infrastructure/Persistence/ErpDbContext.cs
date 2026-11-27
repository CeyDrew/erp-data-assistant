using ErpAssistant.Application.Abstractions;
using ErpAssistant.Domain.Ai;
using ErpAssistant.Domain.Billing;
using ErpAssistant.Domain.Catalog;
using ErpAssistant.Domain.Documents;
using ErpAssistant.Domain.Inventory;
using ErpAssistant.Domain.Organization;
using ErpAssistant.Domain.Sales;
using ErpAssistant.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ErpAssistant.Infrastructure.Persistence;

public class ErpDbContext(DbContextOptions<ErpDbContext> options, ICurrentUser currentUser)
    : IdentityDbContext<AppUser>(options), IErpDbContext
{
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
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PersonalAccessToken> PersonalAccessTokens => Set<PersonalAccessToken>();

    // Global query filter'lar bu iki özelliği okur. EF Core, DbContext üyelerine yapılan başvuruları
    // her sorguda parametre olarak yeniden değerlendirir; model bir kez derlenir, değer her istekte değişir.
    internal int? RepFilter => AccessPolicy.RowFilterFor(currentUser);
    internal IReadOnlyCollection<string>? DepartmentFilter => AccessPolicy.DepartmentsFor(currentUser);
    internal bool DepartmentFilterOff => DepartmentFilter is null;
    internal string[] AllowedDepartments => DepartmentFilter?.ToArray() ?? [];

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Türkçe büyük/küçük harf duyarsız arama: "istanbul" = "İSTANBUL", "ışık" = "IŞIK".
        modelBuilder.UseCollation("Turkish_CI_AS");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ErpDbContext).Assembly);

        // Satır bazlı yetki. Satış temsilcisi yalnızca kendi müşterilerini, siparişlerini, faturalarını
        // ve tahsilatlarını görür. AI ve MCP araçları da bu bağlamı kullandığından filtre atlanamaz.
        modelBuilder.Entity<Customer>().HasQueryFilter(c => RepFilter == null || c.SalesRepresentativeId == RepFilter);
        modelBuilder.Entity<Order>().HasQueryFilter(o => RepFilter == null || o.SalesRepresentativeId == RepFilter);
        modelBuilder.Entity<OrderLine>().HasQueryFilter(l => RepFilter == null || l.Order.SalesRepresentativeId == RepFilter);
        modelBuilder.Entity<Invoice>().HasQueryFilter(i => RepFilter == null || i.SalesRepresentativeId == RepFilter);
        modelBuilder.Entity<Payment>().HasQueryFilter(p => RepFilter == null || p.Invoice.SalesRepresentativeId == RepFilter);

        // Doküman görünürlüğü departmana göre (vektör aramasında da aynı liste kullanılır).
        modelBuilder.Entity<Document>().HasQueryFilter(d => DepartmentFilterOff || AllowedDepartments.Contains(d.Department.Code));
        modelBuilder.Entity<DocumentChunk>().HasQueryFilter(c => DepartmentFilterOff || AllowedDepartments.Contains(c.Document.Department.Code));
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<string>().HaveMaxLength(256);
    }
}
