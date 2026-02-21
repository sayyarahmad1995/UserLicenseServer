using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Config;

public class LicenseConfig : IEntityTypeConfiguration<License>
{
    public void Configure(EntityTypeBuilder<License> builder)
    {
        builder.ToTable("Licenses");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.LicenseKey).IsRequired().HasMaxLength(200);
        builder.Property(l => l.CreatedAt).IsRequired();
        builder.Property(l => l.ExpiresAt).IsRequired();
        builder.Property(l => l.Status).HasConversion<string>().IsRequired();

        builder.HasIndex(l => new { l.UserId, l.Status, l.ExpiresAt });

        builder.HasIndex(l => new { l.UserId, l.Status })
           .IsUnique()
           .HasDatabaseName("IX_Licenses_UserId_Status_Active")
           .HasFilter("\"Status\" = 'Active'");

        // Unique index on LicenseKey to prevent duplicates and enable O(1) lookup
        builder.HasIndex(l => l.LicenseKey)
           .IsUnique()
           .HasDatabaseName("IX_Licenses_LicenseKey");
    }
}
