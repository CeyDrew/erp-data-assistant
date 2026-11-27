using ErpAssistant.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpAssistant.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.Property(x => x.InvoiceNumber).HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<byte>();
        b.Ignore(x => x.Outstanding);
        b.HasIndex(x => x.InvoiceNumber).IsUnique();
        b.HasIndex(x => x.OrderId).IsUnique();

        // Geciken tahsilatlar: açık faturalar vadeye göre.
        b.HasIndex(x => new { x.Status, x.DueDate })
            .IncludeProperties(x => new { x.CustomerId, x.SalesRepresentativeId, x.TotalAmount, x.PaidAmount });

        b.HasIndex(x => new { x.CustomerId, x.IssueDate });
        b.HasIndex(x => new { x.SalesRepresentativeId, x.IssueDate });

        b.HasOne(x => x.Order).WithOne(o => o.Invoice).HasForeignKey<Invoice>(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Customer).WithMany().OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SalesRepresentative).WithMany().OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Payments).WithOne(p => p.Invoice).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.Property(x => x.Method).HasConversion<byte>();
        b.HasIndex(x => new { x.InvoiceId, x.PaidAt });
    }
}
