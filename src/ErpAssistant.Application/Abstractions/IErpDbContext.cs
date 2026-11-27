using ErpAssistant.Domain.Ai;
using ErpAssistant.Domain.Billing;
using ErpAssistant.Domain.Catalog;
using ErpAssistant.Domain.Documents;
using ErpAssistant.Domain.Field;
using ErpAssistant.Domain.Inventory;
using ErpAssistant.Domain.Organization;
using ErpAssistant.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace ErpAssistant.Application.Abstractions;

/// <summary>
/// Application katmanının veriye eriştiği tek kapı. Satır yetkisi filtreleri bu bağlamın
/// arkasındaki global query filter'larda olduğundan, buradan yapılan her sorgu (REST, AI aracı,
/// MCP aracı) kullanıcının yetkisiyle sınırlıdır.
/// </summary>
public interface IErpDbContext
{
    DbSet<Department> Departments { get; }
    DbSet<SalesRepresentative> SalesRepresentatives { get; }
    DbSet<ProductCategory> ProductCategories { get; }
    DbSet<Product> Products { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<StockBalance> StockBalances { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderLine> OrderLines { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Document> Documents { get; }
    DbSet<DocumentChunk> DocumentChunks { get; }
    DbSet<AiQueryLog> AiQueryLogs { get; }
    DbSet<Driver> Drivers { get; }
    DbSet<Visit> Visits { get; }
    DbSet<Delivery> Deliveries { get; }
    DbSet<LocationPing> LocationPings { get; }
    DbSet<Device> Devices { get; }
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }
    DbSet<FieldTombstone> FieldTombstones { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
