using ErpAssistant.Domain.Ai;
using ErpAssistant.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpAssistant.Infrastructure.Persistence.Configurations;

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> b)
    {
        b.Property(x => x.FileName).HasMaxLength(260);
        b.Property(x => x.ContentType).HasMaxLength(128);
        b.Property(x => x.StoragePath).HasMaxLength(512);
        b.Property(x => x.Error).HasMaxLength(2000);
        b.Property(x => x.Status).HasConversion<byte>();
        b.HasIndex(x => new { x.DepartmentId, x.Status });
        b.HasOne(x => x.Department).WithMany().OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Chunks).WithOne(c => c.Document).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> b)
    {
        b.HasIndex(x => new { x.DocumentId, x.ChunkIndex }).IsUnique();
        b.HasIndex(x => x.VectorId).IsUnique();
    }
}

internal sealed class AiQueryLogConfiguration : IEntityTypeConfiguration<AiQueryLog>
{
    public void Configure(EntityTypeBuilder<AiQueryLog> b)
    {
        b.Property(x => x.UserId).HasMaxLength(64);
        b.Property(x => x.UserName).HasMaxLength(128);
        b.Property(x => x.Channel).HasMaxLength(16);
        b.Property(x => x.Question).HasMaxLength(4000);
        b.Property(x => x.Answer).HasMaxLength(-1);
        b.Property(x => x.ToolCallsJson).HasMaxLength(-1);
        b.Property(x => x.Model).HasMaxLength(128);
        b.Property(x => x.Error).HasMaxLength(2000);
        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}
