using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Config;

public class LicenseActivationConfig : IEntityTypeConfiguration<LicenseActivation>
{
    public void Configure(EntityTypeBuilder<LicenseActivation> builder)
    {
        builder.ToTable("LicenseActivations");
        builder.HasKey(la => la.Id);

        builder.Property(la => la.MachineFingerprint)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(la => la.Hostname)
            .HasMaxLength(256);

        builder.Property(la => la.IpAddress)
            .HasMaxLength(45); // IPv6 max length

        builder.Property(la => la.ActivatedAt).IsRequired();
        builder.Property(la => la.LastSeenAt).IsRequired();

        // Ignore computed property
        builder.Ignore(la => la.IsActive);

        builder.HasOne(la => la.License)
            .WithMany(l => l.Activations)
            .HasForeignKey(la => la.LicenseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Only one active activation per license + fingerprint
        builder.HasIndex(la => new { la.LicenseId, la.MachineFingerprint })
            .HasDatabaseName("IX_LicenseActivations_LicenseId_Fingerprint");

        builder.HasIndex(la => la.LicenseId)
            .HasDatabaseName("IX_LicenseActivations_LicenseId");
    }
}
