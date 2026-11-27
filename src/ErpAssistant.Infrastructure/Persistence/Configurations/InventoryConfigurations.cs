using ErpAssistant.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpAssistant.Infrastructure.Persistence.Configurations;

internal sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> b)
    {
        b.Property(x => x.Code).HasMaxLength(16);
        b.Property(x => x.Name).HasMaxLength(64);
        b.Property(x => x.City).HasMaxLength(64);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.Property(x => x.Type).HasConversion<byte>();
        b.Property(x => x.Reference).HasMaxLength(64);

        // Ürün geçmişi ekranı: ürün + depo, tarihe göre sıralı.
        b.HasIndex(x => new { x.ProductId, x.WarehouseId, x.OccurredAt })
            .IncludeProperties(x => new { x.Quantity, x.Type });

        b.HasOne(x => x.Product).WithMany().OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Warehouse).WithMany().OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> b)
    {
        // Migration'da SCHEMABINDING + benzersiz clustered index ile oluşturulan indekslenmiş view.
        b.ToView("vw_StockBalance");
        b.HasKey(x => new { x.ProductId, x.WarehouseId });
    }
}
