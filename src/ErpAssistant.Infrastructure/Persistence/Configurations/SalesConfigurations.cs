using ErpAssistant.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpAssistant.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.Property(x => x.Code).HasMaxLength(16);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.TaxNumber).HasMaxLength(11);
        b.Property(x => x.City).HasMaxLength(64);
        b.Property(x => x.Phone).HasMaxLength(32);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.TaxNumber).IsUnique();

        // Satır filtresi (temsilci) + ad ile listeleme.
        b.HasIndex(x => new { x.SalesRepresentativeId, x.Name });

        b.HasOne(x => x.SalesRepresentative).WithMany(r => r.Customers).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.Property(x => x.OrderNumber).HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<byte>();
        b.HasIndex(x => x.OrderNumber).IsUnique();

        // Dönemsel satış özetleri: tarih aralığı taraması, kalan kolonlar index'ten okunur.
        b.HasIndex(x => x.OrderDate)
            .IncludeProperties(x => new { x.Status, x.TotalAmount, x.CustomerId, x.SalesRepresentativeId })
            .HasDatabaseName("IX_Orders_OrderDate_Covering");

        // Müşterinin açık siparişleri.
        b.HasIndex(x => new { x.CustomerId, x.Status, x.OrderDate });

        // Temsilci satır filtresi + tarih.
        b.HasIndex(x => new { x.SalesRepresentativeId, x.OrderDate })
            .IncludeProperties(x => new { x.Status, x.TotalAmount });

        b.HasOne(x => x.Customer).WithMany().OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SalesRepresentative).WithMany().OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Warehouse).WithMany().OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne(l => l.Order).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> b)
    {
        b.Property(x => x.DiscountRate).HasPrecision(5, 4);

        // "En çok satanlar": ürün bazında miktar ve tutar toplamı.
        b.HasIndex(x => x.ProductId).IncludeProperties(x => new { x.OrderId, x.Quantity, x.LineTotal });

        b.HasOne(x => x.Product).WithMany().OnDelete(DeleteBehavior.Restrict);
    }
}
