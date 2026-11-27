using ErpAssistant.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpAssistant.Infrastructure.Persistence.Configurations;

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.Property(x => x.Code).HasMaxLength(16);
        b.Property(x => x.Name).HasMaxLength(64);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class SalesRepresentativeConfiguration : IEntityTypeConfiguration<SalesRepresentative>
{
    public void Configure(EntityTypeBuilder<SalesRepresentative> b)
    {
        b.Property(x => x.FullName).HasMaxLength(128);
        b.Property(x => x.Region).HasMaxLength(64);
        b.HasIndex(x => x.Email).IsUnique();
        b.HasOne(x => x.Department).WithMany().OnDelete(DeleteBehavior.Restrict);
    }
}
