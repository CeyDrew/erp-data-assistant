using ErpAssistant.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpAssistant.Infrastructure.Persistence.Configurations;

internal sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> b)
    {
        b.Property(x => x.Name).HasMaxLength(64);
        b.HasIndex(x => x.Name).IsUnique();
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.Property(x => x.Sku).HasMaxLength(32);
        b.Property(x => x.Name).HasMaxLength(160);
        b.Property(x => x.Unit).HasMaxLength(16);
        b.HasIndex(x => x.Sku).IsUnique();
        b.Property(x => x.Barcode).HasMaxLength(13).IsUnicode(false);
        b.HasIndex(x => x.Barcode).IsUnique().HasFilter("[Barcode] IS NOT NULL");
        b.HasIndex(x => x.CategoryId);
        b.HasIndex(x => x.Name);
        b.HasOne(x => x.Category).WithMany(c => c.Products).OnDelete(DeleteBehavior.Restrict);
    }
}
