using ErpAssistant.Domain.Field;
using ErpAssistant.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpAssistant.Infrastructure.Persistence.Configurations;

internal sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> b)
    {
        b.Property(x => x.FullName).HasMaxLength(128);
        b.Property(x => x.Phone).HasMaxLength(32);
        b.Property(x => x.City).HasMaxLength(64);
        b.HasIndex(x => x.Email).IsUnique();
    }
}

internal sealed class VisitConfiguration : IEntityTypeConfiguration<Visit>
{
    public void Configure(EntityTypeBuilder<Visit> b)
    {
        b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.Note).HasMaxLength(Visit.MaxNoteLength);

        // Günlük plan: temsilci + gün + sıra benzersiz; eşzamanlı iki planlama denemesinden biri reddedilir.
        b.HasIndex(x => new { x.SalesRepresentativeId, x.PlannedDate, x.Sequence }).IsUnique();
        b.HasIndex(x => x.CustomerId);

        b.HasOne(x => x.SalesRepresentative).WithMany().OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Customer).WithMany().OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public void Configure(EntityTypeBuilder<Delivery> b)
    {
        b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.FailureReason).HasConversion<byte?>();
        b.Property(x => x.RecipientName).HasMaxLength(Delivery.MaxRecipientLength);
        b.Property(x => x.Note).HasMaxLength(Delivery.MaxNoteLength);
        b.Property(x => x.ProofPhotoPath).HasMaxLength(260);
        b.Property(x => x.SignaturePath).HasMaxLength(260);
        b.Ignore(x => x.HasProof);

        b.HasIndex(x => new { x.DriverId, x.PlannedDate, x.Sequence }).IsUnique();
        b.HasIndex(x => x.OrderId);
        b.HasIndex(x => x.SalesRepresentativeId);

        b.HasOne(x => x.Order).WithMany().OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Driver).WithMany().OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class LocationPingConfiguration : IEntityTypeConfiguration<LocationPing>
{
    public void Configure(EntityTypeBuilder<LocationPing> b)
    {
        b.Property(x => x.UserId).HasMaxLength(450);
        b.HasIndex(x => x.ClientId).IsUnique();
        b.HasIndex(x => new { x.UserId, x.RecordedAt });
    }
}

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> b)
    {
        b.Property(x => x.UserId).HasMaxLength(450);
        b.Property(x => x.InstallationId).HasMaxLength(64);
        b.Property(x => x.Platform).HasMaxLength(16);
        b.Property(x => x.AppVersion).HasMaxLength(32);
        b.Property(x => x.PushToken).HasMaxLength(512);
        b.HasIndex(x => new { x.UserId, x.InstallationId }).IsUnique();
    }
}

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> b)
    {
        b.HasKey(x => x.Key);
        b.Property(x => x.UserId).HasMaxLength(450);
        b.Property(x => x.Operation).HasMaxLength(32);
        b.Property(x => x.RequestHash).HasMaxLength(64).IsFixedLength();
        b.Property(x => x.ResponseJson).HasMaxLength(-1);
        b.HasIndex(x => x.CreatedAt);
    }
}

internal sealed class FieldTombstoneConfiguration : IEntityTypeConfiguration<FieldTombstone>
{
    public void Configure(EntityTypeBuilder<FieldTombstone> b)
    {
        b.Property(x => x.Entity).HasMaxLength(16);
        b.HasIndex(x => x.SalesRepresentativeId);
        b.HasIndex(x => x.DriverId);
    }
}
