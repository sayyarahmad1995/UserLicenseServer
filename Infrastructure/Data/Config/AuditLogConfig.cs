using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Config;

public class AuditLogConfig : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).IsRequired().HasMaxLength(100);
        builder.Property(a => a.EntityType).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Details).HasMaxLength(2000);
        builder.Property(a => a.IpAddress).HasMaxLength(50);
        builder.Property(a => a.Timestamp).IsRequired();

        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => new { a.Action, a.EntityType });
    }
}
