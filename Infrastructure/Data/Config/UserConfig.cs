using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Config
{
   public class UserConfig : IEntityTypeConfiguration<User>
   {
      public void Configure(EntityTypeBuilder<User> builder)
      {
         builder.ToTable("Users");
         builder.HasKey(u => u.Id);
         builder.Property(u => u.Username).IsRequired().HasMaxLength(100);
         builder.Property(u => u.Email).IsRequired().HasMaxLength(200);
         builder.Property(u => u.PasswordHash).IsRequired();
         builder.Property(u => u.Role).IsRequired().HasMaxLength(50);
         builder.Property(u => u.Status).HasConversion<string>().IsRequired();
         builder.HasMany(u => u.Licenses).WithOne(l => l.User).HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
      }
   }
}
